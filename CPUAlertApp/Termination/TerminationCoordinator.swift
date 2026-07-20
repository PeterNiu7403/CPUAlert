import AppKit
import Darwin
import Foundation

protocol SignalSending: Sendable {
    func send(signal: Int32, to pid: Int32) -> Int32
}

struct DarwinSignalSender: SignalSending {
    func send(signal: Int32, to pid: Int32) -> Int32 {
        Darwin.kill(pid, signal) == 0 ? 0 : errno
    }
}

protocol PrivilegedTerminationServing: Sendable {
    func terminate(identity: ProcessIdentity, signal: Int32) async -> Int32
}

enum TerminationResult: Equatable, Sendable {
    case terminated
    case forceAvailable
    case identityChanged
    case forceNotAvailable
    case protectedTarget
    case notFound
    case failed(Int32)
}

struct MemoryCleanupOutcome: Identifiable, Equatable, Sendable {
    var id: ProcessIdentity { target.identity }
    let target: ProcessMetric
    let result: TerminationResult
}

enum MemoryCleanupPolicy {
    static func candidates(
        from processes: [ProcessMetric],
        currentUID: UInt32 = getuid()
    ) -> [ProcessMetric] {
        var seen: Set<ProcessIdentity> = []
        return processes.filter { process in
            guard seen.insert(process.identity).inserted,
                  process.ownerUID == currentUID,
                  process.isApplication,
                  process.physicalFootprintBytes > 0 else { return false }
            return !ProtectedProcessPolicy.isProtected(
                pid: process.identity.pid,
                name: process.name
            )
        }.sorted {
            $0.physicalFootprintBytes == $1.physicalFootprintBytes
                ? $0.identity.pid < $1.identity.pid
                : $0.physicalFootprintBytes > $1.physicalFootprintBytes
        }
    }

    static func estimatedFootprint(of processes: [ProcessMetric]) -> UInt64 {
        processes.reduce(into: UInt64.zero) { result, process in
            let addition = result.addingReportingOverflow(process.physicalFootprintBytes)
            result = addition.overflow ? .max : addition.partialValue
        }
    }
}

actor TerminationCoordinator {
    private let identityReader: any ProcessIdentityReading
    private let signalSender: any SignalSending
    private let privilegedTerminator: any PrivilegedTerminationServing
    private let gracePeriod: Duration
    private var forceEligible: Set<ProcessIdentity> = []

    init(
        identityReader: any ProcessIdentityReading = ProcessIdentityReader(),
        signalSender: any SignalSending = DarwinSignalSender(),
        privilegedTerminator: any PrivilegedTerminationServing,
        gracePeriod: Duration = .seconds(3)
    ) {
        self.identityReader = identityReader
        self.signalSender = signalSender
        self.privilegedTerminator = privilegedTerminator
        self.gracePeriod = gracePeriod
    }

    func requestGraceful(_ target: ProcessMetric) async -> TerminationResult {
        forceEligible.remove(target.identity)
        guard let current = identityReader.currentIdentity(pid: target.identity.pid) else {
            return .notFound
        }
        guard current.identity == target.identity else { return .identityChanged }
        guard !ProtectedProcessPolicy.isProtected(
            pid: current.identity.pid,
            name: current.name
        ) else {
            return .protectedTarget
        }

        let errorCode: Int32
        if current.uid == getuid() {
            if target.isApplication,
               let application = await MainActor.run(body: {
                   NSRunningApplication(processIdentifier: current.identity.pid)
               }) {
                let accepted = await MainActor.run { application.terminate() }
                errorCode = accepted ? 0 : EPERM
            } else {
                errorCode = signalSender.send(signal: SIGTERM, to: current.identity.pid)
            }
        } else {
            errorCode = await privilegedTerminator.terminate(
                identity: current.identity,
                signal: SIGTERM
            )
        }
        guard errorCode == 0 else { return .failed(errorCode) }

        do {
            try await Task.sleep(for: gracePeriod)
        } catch {
            return .failed(ECANCELED)
        }
        guard let remaining = identityReader.currentIdentity(pid: target.identity.pid),
              remaining.identity == target.identity else {
            return .terminated
        }
        forceEligible.insert(target.identity)
        return .forceAvailable
    }

    func requestForce(_ target: ProcessMetric) async -> TerminationResult {
        guard forceEligible.remove(target.identity) != nil else {
            return .forceNotAvailable
        }
        guard let current = identityReader.currentIdentity(pid: target.identity.pid) else {
            return .notFound
        }
        guard current.identity == target.identity else { return .identityChanged }
        guard !ProtectedProcessPolicy.isProtected(
            pid: current.identity.pid,
            name: current.name
        ) else {
            return .protectedTarget
        }

        let errorCode = current.uid == getuid()
            ? signalSender.send(signal: SIGKILL, to: current.identity.pid)
            : await privilegedTerminator.terminate(
                identity: current.identity,
                signal: SIGKILL
            )
        return errorCode == 0 ? .terminated : .failed(errorCode)
    }

    func requestGraceful(_ targets: [ProcessMetric]) async -> [MemoryCleanupOutcome] {
        var outcomes: [MemoryCleanupOutcome] = []
        outcomes.reserveCapacity(targets.count)
        for target in targets {
            guard !Task.isCancelled else { break }
            outcomes.append(MemoryCleanupOutcome(
                target: target,
                result: await requestGraceful(target)
            ))
        }
        return outcomes
    }

    static func leaderTarget(for group: GPUGroupMetric) -> ProcessIdentity? {
        group.leader
    }
}
