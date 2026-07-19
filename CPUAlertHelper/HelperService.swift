import Darwin
import Foundation
import Security

final class HelperService: NSObject, NSXPCListenerDelegate, HelperXPCProtocol,
    @unchecked Sendable
{
    private struct GracefulSignalRecord: Sendable {
        let identity: ProcessIdentity
        let sentAt: ContinuousClock.Instant
    }

    static let appRequirement =
        "identifier \"com.cpualert.app\" and anchor apple generic and certificate leaf[subject.OU] = \"H28G76Z652\""
    private static let removablePaths = [
        "/Library/PrivilegedHelperTools/com.cpualert.helper",
        "/Library/LaunchDaemons/com.cpualert.helper.plist",
    ]

    private let lock = NSLock()
    private let identityReader = ProcessIdentityReader()
    private let clock = ContinuousClock()
    private var gracefulSignals: [Int32: GracefulSignalRecord] = [:]
    private var activeConnections = 0
    private var activeRequests = 0
    private var lastActivity: ContinuousClock.Instant
    private let idleTimer: DispatchSourceTimer

    override init() {
        lastActivity = clock.now
        idleTimer = DispatchSource.makeTimerSource(queue: .global(qos: .utility))
        super.init()
        idleTimer.schedule(deadline: .now() + 1, repeating: 1)
        idleTimer.setEventHandler { [weak self] in
            self?.exitWhenIdle()
        }
        idleTimer.resume()
    }

    func listener(
        _ listener: NSXPCListener,
        shouldAcceptNewConnection connection: NSXPCConnection
    ) -> Bool {
        guard validateCaller(connection) else { return false }

        let lease = ConnectionLease { [weak self] in
            self?.connectionEnded()
        }
        connection.exportedInterface = helperInterface()
        connection.exportedObject = self
        connection.interruptionHandler = {
            lease.finish()
        }
        connection.invalidationHandler = {
            lease.finish()
        }
        lock.locked {
            activeConnections += 1
            lastActivity = clock.now
        }
        connection.resume()
        return true
    }

    func perform(
        _ request: HelperRequest,
        withReply reply: @escaping (HelperResponse) -> Void
    ) {
        beginRequest()
        let errorCode: Int32
        let shouldExit: Bool
        switch request.operation {
        case .terminate:
            errorCode = terminate(request)
            shouldExit = false
        case .uninstall:
            errorCode = uninstall(request)
            shouldExit = true
        }

        reply(HelperResponse(errorCode: errorCode))
        endRequest()
        if shouldExit {
            DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + 0.2) {
                Darwin.exit(EXIT_SUCCESS)
            }
        }
    }

    private func terminate(_ request: HelperRequest) -> Int32 {
        guard request.signal == SIGTERM || request.signal == SIGKILL,
              request.pid > 1,
              request.startTimeNanoseconds > 0 else {
            return EINVAL
        }
        guard let current = identityReader.currentIdentity(pid: request.pid) else {
            removeGracefulRecord(pid: request.pid)
            return ESRCH
        }
        guard current.identity.startTimeNanoseconds == request.startTimeNanoseconds else {
            removeGracefulRecord(pid: request.pid)
            return ESRCH
        }
        guard !ProtectedProcessPolicy.isProtected(pid: current.identity.pid, name: current.name) else {
            removeGracefulRecord(pid: request.pid)
            return EPERM
        }

        if request.signal == SIGKILL {
            let allowed = lock.locked { () -> Bool in
                guard let record = gracefulSignals.removeValue(forKey: request.pid),
                      record.identity == current.identity else {
                    return false
                }
                return record.sentAt.duration(to: clock.now) >= .seconds(3)
            }
            guard allowed else { return EACCES }
        }

        guard Darwin.kill(request.pid, request.signal) == 0 else {
            return errno
        }
        if request.signal == SIGTERM {
            lock.locked {
                gracefulSignals[request.pid] = GracefulSignalRecord(
                    identity: current.identity,
                    sentAt: clock.now
                )
            }
        }
        return 0
    }

    private func uninstall(_ request: HelperRequest) -> Int32 {
        guard request.pid == 0,
              request.startTimeNanoseconds == 0,
              request.signal == 0 else {
            return EINVAL
        }

        var firstError: Int32 = 0
        for path in Self.removablePaths where Darwin.unlink(path) != 0 {
            let currentError = errno
            if currentError != ENOENT, firstError == 0 {
                firstError = currentError
            }
        }
        return firstError
    }

    private func validateCaller(_ connection: NSXPCConnection) -> Bool {
        // The listener applies the same requirement to the XPC peer before this
        // delegate method runs. Repeat the validity check here as defense in depth.
        let attributes = [
            kSecGuestAttributePid: NSNumber(value: connection.processIdentifier)
        ] as CFDictionary

        var code: SecCode?
        guard SecCodeCopyGuestWithAttributes(
            nil,
            attributes,
            SecCSFlags(),
            &code
        ) == errSecSuccess,
        let code else {
            return false
        }

        var requirement: SecRequirement?
        guard SecRequirementCreateWithString(
            Self.appRequirement as CFString,
            SecCSFlags(),
            &requirement
        ) == errSecSuccess,
        let requirement else {
            return false
        }

        return SecCodeCheckValidity(
            code,
            SecCSFlags(rawValue: kSecCSStrictValidate),
            requirement
        ) == errSecSuccess
    }

    private func removeGracefulRecord(pid: Int32) {
        _ = lock.locked {
            gracefulSignals.removeValue(forKey: pid)
        }
    }

    private func beginRequest() {
        lock.locked {
            activeRequests += 1
            lastActivity = clock.now
        }
    }

    private func endRequest() {
        lock.locked {
            activeRequests = max(0, activeRequests - 1)
            lastActivity = clock.now
        }
    }

    private func connectionEnded() {
        lock.locked {
            activeConnections = max(0, activeConnections - 1)
            lastActivity = clock.now
        }
    }

    private func exitWhenIdle() {
        let shouldExit = lock.locked {
            activeConnections == 0
                && activeRequests == 0
                && lastActivity.duration(to: clock.now) >= .seconds(15)
        }
        if shouldExit {
            Darwin.exit(EXIT_SUCCESS)
        }
    }
}

private final class ConnectionLease: @unchecked Sendable {
    private let lock = NSLock()
    private var isFinished = false
    private let onFinish: @Sendable () -> Void

    init(onFinish: @escaping @Sendable () -> Void) {
        self.onFinish = onFinish
    }

    func finish() {
        let shouldFinish = lock.locked {
            guard !isFinished else { return false }
            isFinished = true
            return true
        }
        if shouldFinish {
            onFinish()
        }
    }
}

private extension NSLock {
    func locked<Result>(_ body: () throws -> Result) rethrows -> Result {
        lock()
        defer { unlock() }
        return try body()
    }
}
