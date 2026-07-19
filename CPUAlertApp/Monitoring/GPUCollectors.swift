import AppKit
import Foundation

struct GPUResidency: Equatable, Sendable {
    let active: UInt64
    let total: UInt64
}

private final class GPUContextHandle: @unchecked Sendable {
    let pointer: UnsafeMutableRawPointer?

    init() {
        pointer = CPUACreateGPUContext()
    }

    deinit {
        if let pointer {
            CPUADestroyGPUContext(pointer)
        }
    }
}

actor SystemGPUCollector: GPUCollecting {
    private let context = GPUContextHandle()
    private let coalitionCollector = CoalitionGPUCollector()

    static func aggregate(_ rows: [GPUResidency]) -> Double? {
        var active: UInt64 = 0
        var total: UInt64 = 0
        for row in rows {
            let activeResult = active.addingReportingOverflow(row.active)
            let totalResult = total.addingReportingOverflow(row.total)
            guard !activeResult.overflow, !totalResult.overflow else { return nil }
            active = activeResult.partialValue
            total = totalResult.partialValue
        }
        guard total > 0, active <= total else { return nil }
        return Double(active) / Double(total)
    }

    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource) {
        guard let context = context.pointer else { return (nil, .unavailable) }
        var sample = CPUAGPUSample()
        guard CPUACopyGPUSample(context, &sample), sample.usage.isFinite else {
            return (nil, .unavailable)
        }
        let source: GPUSource = sample.source == CPUA_GPU_SOURCE_IOREPORT
            ? .ioReport
            : sample.source == CPUA_GPU_SOURCE_IOACCELERATOR
                ? .ioAccelerator
                : .unavailable
        return source == .unavailable
            ? (nil, .unavailable)
            : (max(0, min(sample.usage, 1)), source)
    }

    func sampleGroups() async throws -> [GPUGroupMetric] {
        await coalitionCollector.sample()
    }
}

private struct CoalitionMember: Sendable {
    let coalitionID: UInt64
    let identity: ProcessIdentity
    let name: String
    let ownerUID: UInt32
    let isApplication: Bool
}

actor CoalitionGPUCollector {
    private var previous: [UInt64: UInt64] = [:]

    static func shares(
        previous: [UInt64: UInt64],
        current: [UInt64: UInt64]
    ) -> [UInt64: Double] {
        var deltas: [UInt64: UInt64] = [:]
        for (id, value) in current {
            guard let old = previous[id], value >= old else { continue }
            deltas[id] = value - old
        }
        var total: UInt64 = 0
        for delta in deltas.values {
            let result = total.addingReportingOverflow(delta)
            guard !result.overflow else { return [:] }
            total = result.partialValue
        }
        guard total > 0 else { return [:] }
        return deltas.mapValues { Double($0) / Double(total) }
    }

    func sample() async -> [GPUGroupMetric] {
        let members = await Self.copyMembers()
        let grouped = Dictionary(grouping: members, by: \.coalitionID)
        var current: [UInt64: UInt64] = [:]
        for coalitionID in grouped.keys {
            var counter = CPUACoalitionGPUCounter()
            if CPUACopyCoalitionGPUCounter(coalitionID, &counter) {
                current[coalitionID] = counter.gpu_time
            }
        }

        let activityShares = Self.shares(previous: previous, current: current)
        previous = current

        return activityShares.compactMap { coalitionID, share in
            guard let groupMembers = grouped[coalitionID], !groupMembers.isEmpty else {
                return nil
            }
            let oldest = groupMembers.min {
                $0.identity.startTimeNanoseconds < $1.identity.startTimeNanoseconds
            }
            let leader = groupMembers
                .filter(\.isApplication)
                .min { $0.identity.startTimeNanoseconds < $1.identity.startTimeNanoseconds }
            return GPUGroupMetric(
                id: coalitionID,
                name: leader?.name ?? oldest?.name ?? "Process group",
                leader: leader?.identity,
                members: groupMembers
                    .sorted {
                        if $0.isApplication != $1.isApplication {
                            return $0.isApplication && !$1.isApplication
                        }
                        return $0.identity.pid < $1.identity.pid
                    }
                    .map {
                        GPUGroupMemberMetric(
                            identity: $0.identity,
                            name: $0.name,
                            ownerUID: $0.ownerUID,
                            isApplication: $0.isApplication
                        )
                    },
                activityShare: share
            )
        }
        .sorted { $0.activityShare > $1.activityShare }
        .prefix(10)
        .map { $0 }
    }

    @MainActor
    private static func copyMembers() -> [CoalitionMember] {
        let requiredBytes = Int(CPUACopyAllPIDs(nil, 0))
        guard requiredBytes > 0 else { return [] }
        let stride = MemoryLayout<pid_t>.stride
        var pids = [pid_t](
            repeating: 0,
            count: max((requiredBytes + stride - 1) / stride, 1)
        )
        var filledBytes = pids.withUnsafeMutableBufferPointer { buffer in
            CPUACopyAllPIDs(buffer.baseAddress, Int32(clamping: buffer.count * stride))
        }
        if filledBytes > pids.count * stride {
            pids = [pid_t](
                repeating: 0,
                count: (Int(filledBytes) + stride - 1) / stride
            )
            filledBytes = pids.withUnsafeMutableBufferPointer { buffer in
                CPUACopyAllPIDs(buffer.baseAddress, Int32(clamping: buffer.count * stride))
            }
        }
        guard filledBytes > 0 else { return [] }

        return pids.prefix(min(Int(filledBytes) / stride, pids.count)).compactMap { pid in
            guard pid > 0 else { return nil }
            var process = CPUAProcessCounter()
            var coalitionID: UInt64 = 0
            guard CPUACopyProcessCounter(pid, &process),
                  CPUACopyProcessCoalitionID(pid, &coalitionID),
                  coalitionID > 0 else { return nil }
            let running = NSRunningApplication(processIdentifier: pid)
            let counterName = processName(from: &process.name)
            return CoalitionMember(
                coalitionID: coalitionID,
                identity: ProcessIdentity(
                    pid: pid,
                    startTimeNanoseconds: process.start_time_ns
                ),
                name: running?.localizedName
                    ?? (counterName.isEmpty ? "PID \(pid)" : counterName),
                ownerUID: process.uid,
                isApplication: running?.activationPolicy == .regular
            )
        }
    }
}

private func processName<Value>(from value: inout Value) -> String {
    withUnsafeBytes(of: &value) { bytes in
        guard let baseAddress = bytes.bindMemory(to: CChar.self).baseAddress else {
            return ""
        }
        return String(cString: baseAddress)
    }
}
