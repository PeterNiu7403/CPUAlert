import Darwin
import Foundation
import Testing
@testable import CPUAlert

private final class FakeIdentityReader: ProcessIdentityReading, @unchecked Sendable {
    var record: ProcessIdentityRecord?

    init(record: ProcessIdentityRecord?) {
        self.record = record
    }

    func currentIdentity(pid: Int32) -> ProcessIdentityRecord? {
        record
    }
}

private final class FakeSignalSender: SignalSending, @unchecked Sendable {
    private(set) var signals: [Int32] = []

    func send(signal: Int32, to pid: Int32) -> Int32 {
        signals.append(signal)
        return 0
    }
}

private struct RejectingPrivilegedTerminator: PrivilegedTerminationServing {
    func terminate(identity: ProcessIdentity, signal: Int32) async -> Int32 {
        EPERM
    }
}

struct TerminationCoordinatorTests {
    @Test func gracefulTerminatesDisposableChild() async throws {
        let child = Process()
        child.executableURL = URL(fileURLWithPath: "/bin/sleep")
        child.arguments = ["30"]
        try child.run()
        defer {
            if child.isRunning {
                Darwin.kill(child.processIdentifier, SIGKILL)
            }
        }

        let identityReader = ProcessIdentityReader()
        var observed: ProcessIdentityRecord?
        for _ in 0..<50 {
            if let record = identityReader.currentIdentity(pid: child.processIdentifier) {
                observed = record
                break
            }
            try await Task.sleep(for: .milliseconds(10))
        }
        let record = try #require(observed)
        let target = ProcessMetric(
            identity: record.identity,
            name: record.name,
            bundleIdentifier: nil,
            ownerUID: record.uid,
            cpuUsage: 0,
            isApplication: false
        )
        let coordinator = TerminationCoordinator(
            identityReader: identityReader,
            signalSender: DarwinSignalSender(),
            privilegedTerminator: RejectingPrivilegedTerminator(),
            gracePeriod: .milliseconds(50)
        )

        #expect(await coordinator.requestGraceful(target) == .terminated)
        child.waitUntilExit()
        #expect(child.terminationReason == .uncaughtSignal)
        #expect(child.terminationStatus == SIGTERM)
    }

    @Test func changedStartTimeIsRejected() async {
        let selected = Self.metric(startTime: 10)
        let reader = FakeIdentityReader(record: Self.record(startTime: 11))
        let sender = FakeSignalSender()
        let coordinator = TerminationCoordinator(
            identityReader: reader,
            signalSender: sender,
            privilegedTerminator: RejectingPrivilegedTerminator(),
            gracePeriod: .zero
        )

        #expect(await coordinator.requestGraceful(selected) == .identityChanged)
        #expect(sender.signals.isEmpty)
    }

    @Test func gracefulAndForceAreSeparateActions() async {
        let selected = Self.metric(startTime: 10)
        let reader = FakeIdentityReader(record: Self.record(startTime: 10))
        let sender = FakeSignalSender()
        let coordinator = TerminationCoordinator(
            identityReader: reader,
            signalSender: sender,
            privilegedTerminator: RejectingPrivilegedTerminator(),
            gracePeriod: .zero
        )

        #expect(await coordinator.requestGraceful(selected) == .forceAvailable)
        #expect(sender.signals == [SIGTERM])
        #expect(await coordinator.requestForce(selected) == .terminated)
        #expect(sender.signals == [SIGTERM, SIGKILL])
    }

    @Test func forceWithoutGracefulRequestIsDenied() async {
        let selected = Self.metric(startTime: 10)
        let sender = FakeSignalSender()
        let coordinator = TerminationCoordinator(
            identityReader: FakeIdentityReader(record: Self.record(startTime: 10)),
            signalSender: sender,
            privilegedTerminator: RejectingPrivilegedTerminator(),
            gracePeriod: .zero
        )

        #expect(await coordinator.requestForce(selected) == .forceNotAvailable)
        #expect(sender.signals.isEmpty)
    }

    private static func metric(startTime: UInt64) -> ProcessMetric {
        ProcessMetric(
            identity: ProcessIdentity(pid: 42_424, startTimeNanoseconds: startTime),
            name: "CPUStress",
            bundleIdentifier: nil,
            ownerUID: getuid(),
            cpuUsage: 0.8,
            isApplication: false
        )
    }

    private static func record(startTime: UInt64) -> ProcessIdentityRecord {
        ProcessIdentityRecord(
            identity: ProcessIdentity(pid: 42_424, startTimeNanoseconds: startTime),
            name: "CPUStress",
            executablePath: "/tmp/CPUStress",
            uid: getuid()
        )
    }
}
