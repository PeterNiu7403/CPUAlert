import AppKit
import Dispatch
import Foundation

struct SystemCPUTicks: Equatable, Sendable {
    let user: UInt64
    let system: UInt64
    let idle: UInt64
    let nice: UInt64
}

actor SystemCPUCollector: SystemCPUCollecting {
    private var previous: SystemCPUTicks?

    static func usage(previous: SystemCPUTicks, current: SystemCPUTicks) -> Double? {
        guard current.user >= previous.user,
              current.system >= previous.system,
              current.idle >= previous.idle,
              current.nice >= previous.nice else { return nil }
        let busy = current.user - previous.user
            + current.system - previous.system
            + current.nice - previous.nice
        let total = busy + current.idle - previous.idle
        guard total > 0 else { return nil }
        return max(0, min(Double(busy) / Double(total), 1))
    }

    func sampleSystemCPU() async throws -> Double? {
        var raw = CPUASystemTicks()
        guard CPUACopySystemTicks(&raw) else { return nil }
        let current = SystemCPUTicks(
            user: raw.user,
            system: raw.system,
            idle: raw.idle,
            nice: raw.nice
        )
        defer { previous = current }
        guard let previous else { return nil }
        return Self.usage(previous: previous, current: current)
    }
}

struct SystemMemoryStatistics: Equatable, Sendable {
    let pageSize: UInt64
    let activePages: UInt64
    let wiredPages: UInt64
    let compressedPages: UInt64
}

actor SystemMemoryCollector: SystemMemoryCollecting {
    static func metric(
        totalBytes: UInt64,
        statistics: SystemMemoryStatistics
    ) -> MemoryMetric? {
        guard totalBytes > 0, statistics.pageSize > 0 else { return nil }

        func bytes(for pages: UInt64) -> UInt64 {
            let result = pages.multipliedReportingOverflow(by: statistics.pageSize)
            return result.overflow ? .max : result.partialValue
        }

        func saturatedAdd(_ lhs: UInt64, _ rhs: UInt64) -> UInt64 {
            let result = lhs.addingReportingOverflow(rhs)
            return result.overflow ? .max : result.partialValue
        }

        let activeBytes = bytes(for: statistics.activePages)
        let wiredBytes = bytes(for: statistics.wiredPages)
        let compressedBytes = bytes(for: statistics.compressedPages)
        let pressureBytes = saturatedAdd(
            saturatedAdd(activeBytes, wiredBytes),
            compressedBytes
        )
        return MemoryMetric(
            totalBytes: totalBytes,
            usedBytes: min(pressureBytes, totalBytes),
            compressedBytes: min(compressedBytes, totalBytes)
        )
    }

    func sampleSystemMemory() async throws -> MemoryMetric? {
        var raw = CPUASystemMemoryStatistics()
        guard CPUACopySystemMemoryStatistics(&raw) else { return nil }
        return Self.metric(
            totalBytes: ProcessInfo.processInfo.physicalMemory,
            statistics: SystemMemoryStatistics(
                pageSize: raw.page_size,
                activePages: raw.active_pages,
                wiredPages: raw.wired_pages,
                compressedPages: raw.compressed_pages
            )
        )
    }
}

actor ProcessCPUCollector: ProcessCPUCollecting {
    private struct Baseline: Sendable {
        let cpuNanoseconds: UInt64
        let observedNanoseconds: UInt64
    }

    private struct NumericProcess: Sendable {
        let identity: ProcessIdentity
        let name: String
        let ownerUID: UInt32
        let cpuUsage: Double?
        let physicalFootprintBytes: UInt64
    }

    private var processBaselines: [ProcessIdentity: Baseline] = [:]
    private var threadBaselines: [ProcessIdentity: [UInt64: Baseline]] = [:]
    private var cachedProcesses: [ProcessMetric] = []
    private let logicalCPUCount = max(ProcessInfo.processInfo.activeProcessorCount, 1)
    private let maximumElapsedNanoseconds: UInt64 = 30_000_000_000

    static func normalizedUsage(
        previousNanoseconds: UInt64,
        currentNanoseconds: UInt64,
        elapsedNanoseconds: UInt64,
        logicalCPUCount: Int
    ) -> Double? {
        guard currentNanoseconds >= previousNanoseconds,
              elapsedNanoseconds > 0,
              logicalCPUCount > 0 else { return nil }
        let value = Double(currentNanoseconds - previousNanoseconds)
            / Double(elapsedNanoseconds)
            / Double(logicalCPUCount)
        return value.isFinite ? max(0, min(value, 1)) : nil
    }

    func sampleProcesses() async throws -> [ProcessMetric] {
        try await sampleProcessRankings().cpu
    }

    func sampleProcessRankings() async throws -> ProcessRankingSnapshot {
        let observed = DispatchTime.now().uptimeNanoseconds
        let counters = copyProcessCounters()
        var nextBaselines: [ProcessIdentity: Baseline] = [:]
        var numeric: [NumericProcess] = []
        numeric.reserveCapacity(counters.count)

        for counter in counters {
            let identity = ProcessIdentity(
                pid: counter.pid,
                startTimeNanoseconds: counter.startTimeNanoseconds
            )
            let current = Baseline(
                cpuNanoseconds: counter.cpuNanoseconds,
                observedNanoseconds: observed
            )
            nextBaselines[identity] = current
            let usage: Double?
            if let baseline = processBaselines[identity],
               observed >= baseline.observedNanoseconds {
                let elapsed = observed - baseline.observedNanoseconds
                usage = elapsed <= maximumElapsedNanoseconds
                    ? Self.normalizedUsage(
                    previousNanoseconds: baseline.cpuNanoseconds,
                    currentNanoseconds: current.cpuNanoseconds,
                    elapsedNanoseconds: elapsed,
                    logicalCPUCount: logicalCPUCount
                ) : nil
            } else {
                usage = nil
            }
            numeric.append(NumericProcess(
                identity: identity,
                name: counter.name,
                ownerUID: counter.ownerUID,
                cpuUsage: usage,
                physicalFootprintBytes: counter.physicalFootprintBytes
            ))
        }
        processBaselines = nextBaselines

        let cpuRanked = numeric.compactMap { row -> NumericProcess? in
            row.cpuUsage == nil ? nil : row
        }.sorted {
            $0.cpuUsage == $1.cpuUsage
                ? $0.identity.pid < $1.identity.pid
                : ($0.cpuUsage ?? 0) > ($1.cpuUsage ?? 0)
        }
        let applications = await RunningApplicationCatalog.shared.snapshot()
        func decorate(_ row: NumericProcess) -> ProcessMetric {
            let application = applications[row.identity.pid]
            return ProcessMetric(
                identity: row.identity,
                name: row.name,
                bundleIdentifier: application?.bundleIdentifier,
                ownerUID: row.ownerUID,
                cpuUsage: row.cpuUsage ?? 0,
                physicalFootprintBytes: row.physicalFootprintBytes,
                isApplication: application?.isRegularApplication == true
            )
        }

        cachedProcesses = cpuRanked.prefix(20).map(decorate)
        let memoryRanked = numeric.sorted {
            $0.physicalFootprintBytes == $1.physicalFootprintBytes
                ? $0.identity.pid < $1.identity.pid
                : $0.physicalFootprintBytes > $1.physicalFootprintBytes
        }.prefix(20).map(decorate)
        return ProcessRankingSnapshot(
            cpu: cachedProcesses,
            memory: Array(memoryRanked)
        )
    }

    func sampleThreads(for process: ProcessIdentity) async throws -> [ThreadMetric] {
        var processCounter = CPUAProcessCounter()
        guard CPUACopyProcessCounter(process.pid, &processCounter),
              processCounter.start_time_ns == process.startTimeNanoseconds else {
            threadBaselines.removeAll(keepingCapacity: true)
            return []
        }

        let observed = DispatchTime.now().uptimeNanoseconds
        let counters = copyThreadCounters(pid: process.pid)
        let previous = threadBaselines[process] ?? [:]
        var nextBaselines: [UInt64: Baseline] = [:]
        var metrics: [ThreadMetric] = []
        metrics.reserveCapacity(counters.count)

        for counter in counters {
            let current = Baseline(
                cpuNanoseconds: counter.cpuNanoseconds,
                observedNanoseconds: observed
            )
            nextBaselines[counter.id] = current
            guard let baseline = previous[counter.id],
                  observed >= baseline.observedNanoseconds else { continue }
            let elapsed = observed - baseline.observedNanoseconds
            guard elapsed <= maximumElapsedNanoseconds,
                  let usage = Self.normalizedUsage(
                    previousNanoseconds: baseline.cpuNanoseconds,
                    currentNanoseconds: current.cpuNanoseconds,
                    elapsedNanoseconds: elapsed,
                    logicalCPUCount: logicalCPUCount
                  ) else { continue }
            metrics.append(ThreadMetric(
                id: counter.id,
                process: process,
                name: counter.name.isEmpty ? nil : counter.name,
                cpuUsage: usage
            ))
        }

        var finalIdentityCheck = CPUAProcessCounter()
        guard CPUACopyProcessCounter(process.pid, &finalIdentityCheck),
              finalIdentityCheck.start_time_ns == process.startTimeNanoseconds else {
            threadBaselines.removeAll(keepingCapacity: true)
            return []
        }
        threadBaselines = [process: nextBaselines]
        return Array(metrics.sorted { $0.cpuUsage > $1.cpuUsage }.prefix(10))
    }

    private func copyProcessCounters() -> [ProcessCounter] {
        let requiredBytes = Int(CPUACopyAllPIDs(nil, 0))
        guard requiredBytes > 0 else { return [] }
        let stride = MemoryLayout<pid_t>.stride
        var pids = [pid_t](
            repeating: 0,
            count: max((requiredBytes + stride - 1) / stride, 1)
        )
        var copiedBytes = pids.withUnsafeMutableBufferPointer { buffer in
            CPUACopyAllPIDs(buffer.baseAddress, Int32(clamping: buffer.count * stride))
        }
        if copiedBytes > pids.count * stride {
            pids = [pid_t](
                repeating: 0,
                count: (Int(copiedBytes) + stride - 1) / stride
            )
            copiedBytes = pids.withUnsafeMutableBufferPointer { buffer in
                CPUACopyAllPIDs(buffer.baseAddress, Int32(clamping: buffer.count * stride))
            }
        }
        guard copiedBytes > 0 else { return [] }

        var counters: [ProcessCounter] = []
        for pid in pids.prefix(min(Int(copiedBytes) / stride, pids.count)) where pid > 0 {
            var raw = CPUAProcessCounter()
            guard CPUACopyProcessCounter(pid, &raw) else { continue }
            counters.append(ProcessCounter(
                pid: raw.pid,
                startTimeNanoseconds: raw.start_time_ns,
                cpuNanoseconds: raw.cpu_time_ns,
                physicalFootprintBytes: raw.physical_footprint_bytes,
                ownerUID: raw.uid,
                name: string(from: &raw.name)
            ))
        }
        return counters
    }

    private func copyThreadCounters(pid: pid_t) -> [ThreadCounter] {
        let requiredBytes = Int(CPUACopyThreadIDs(pid, nil, 0))
        guard requiredBytes > 0 else { return [] }
        let stride = MemoryLayout<UInt64>.stride
        var ids = [UInt64](
            repeating: 0,
            count: max((requiredBytes + stride - 1) / stride, 1)
        )
        var copiedBytes = ids.withUnsafeMutableBufferPointer { buffer in
            CPUACopyThreadIDs(pid, buffer.baseAddress, Int32(clamping: buffer.count * stride))
        }
        if copiedBytes > ids.count * stride {
            ids = [UInt64](repeating: 0, count: (Int(copiedBytes) + stride - 1) / stride)
            copiedBytes = ids.withUnsafeMutableBufferPointer { buffer in
                CPUACopyThreadIDs(pid, buffer.baseAddress, Int32(clamping: buffer.count * stride))
            }
        }
        guard copiedBytes > 0 else { return [] }

        var counters: [ThreadCounter] = []
        for id in ids.prefix(min(Int(copiedBytes) / stride, ids.count)) where id != 0 {
            var raw = CPUAThreadCounter()
            guard CPUACopyThreadCounter(pid, id, &raw) else { continue }
            counters.append(ThreadCounter(
                id: raw.thread_id,
                cpuNanoseconds: raw.cpu_time_ns,
                name: string(from: &raw.name)
            ))
        }
        return counters
    }
}

struct RunningApplicationMetadata: Sendable {
    let localizedName: String?
    let bundleIdentifier: String?
    let isRegularApplication: Bool
}

@MainActor
final class RunningApplicationCatalog: NSObject {
    static let shared = RunningApplicationCatalog()

    private var applications: [pid_t: RunningApplicationMetadata] = [:]

    private override init() {
        super.init()
        for application in NSWorkspace.shared.runningApplications {
            record(application)
        }
        let center = NSWorkspace.shared.notificationCenter
        center.addObserver(
            self,
            selector: #selector(applicationDidLaunch(_:)),
            name: NSWorkspace.didLaunchApplicationNotification,
            object: nil
        )
        center.addObserver(
            self,
            selector: #selector(applicationDidTerminate(_:)),
            name: NSWorkspace.didTerminateApplicationNotification,
            object: nil
        )
    }

    func snapshot() -> [pid_t: RunningApplicationMetadata] {
        applications
    }

    @objc
    private func applicationDidLaunch(_ notification: Notification) {
        guard let application = notification.userInfo?[NSWorkspace.applicationUserInfoKey]
            as? NSRunningApplication else { return }
        record(application)
    }

    @objc
    private func applicationDidTerminate(_ notification: Notification) {
        guard let application = notification.userInfo?[NSWorkspace.applicationUserInfoKey]
            as? NSRunningApplication else { return }
        applications.removeValue(forKey: application.processIdentifier)
    }

    private func record(_ application: NSRunningApplication) {
        let pid = application.processIdentifier
        guard pid > 0 else { return }
        applications[pid] = RunningApplicationMetadata(
            localizedName: application.localizedName,
            bundleIdentifier: application.bundleIdentifier,
            isRegularApplication: application.activationPolicy == .regular
        )
    }
}

private struct ProcessCounter {
    let pid: pid_t
    let startTimeNanoseconds: UInt64
    let cpuNanoseconds: UInt64
    let physicalFootprintBytes: UInt64
    let ownerUID: UInt32
    let name: String
}

private struct ThreadCounter {
    let id: UInt64
    let cpuNanoseconds: UInt64
    let name: String
}

private func string<Value>(from value: inout Value) -> String {
    withUnsafeBytes(of: &value) { bytes in
        guard let baseAddress = bytes.bindMemory(to: CChar.self).baseAddress else { return "" }
        return String(cString: baseAddress)
    }
}
