import Darwin
import Foundation
import LocalAuthentication
import Security

enum HelperClientFailure: Error, Sendable {
    case authenticationCancelled
    case installationFailed
    case connectionLost
    case invalidProxy
}

struct HelperCleanupResult: Equatable, Sendable {
    let filesRemoved: Bool
    let registrationRemoved: Bool
}

actor HelperClient: PrivilegedTerminationServing {
    private static let machServiceName = "com.cpualert.helper.xpc"
    private static let helperRequirement =
        "identifier \"com.cpualert.helper\" and anchor apple generic and certificate leaf[subject.OU] = \"H28G76Z652\""
    private static let installedPaths = [
        "/Library/PrivilegedHelperTools/com.cpualert.helper",
        "/Library/LaunchDaemons/com.cpualert.helper.plist",
    ]

    private var connection: NSXPCConnection?
    private var connectionGeneration: UUID?
    private var helperReady = false

    func terminate(identity: ProcessIdentity, signal: Int32) async -> Int32 {
        guard signal == SIGTERM || signal == SIGKILL else { return EINVAL }
        guard await authenticate() else { return ECANCELED }
        do {
            try installIfNeeded()
            let response = try await send(HelperRequest(
                operation: .terminate,
                pid: identity.pid,
                startTimeNanoseconds: identity.startTimeNanoseconds,
                signal: signal
            ))
            return response.errorCode
        } catch HelperClientFailure.authenticationCancelled {
            return ECANCELED
        } catch HelperClientFailure.installationFailed {
            return EPERM
        } catch {
            return ECONNRESET
        }
    }

    func uninstall() async -> HelperCleanupResult {
        guard await authenticate(reason: "CPUAlert needs permission to remove its Root helper.") else {
            return HelperCleanupResult(filesRemoved: false, registrationRemoved: false)
        }

        let helperFilesPresent = Self.installedPaths.contains {
            FileManager.default.fileExists(atPath: $0)
        }
        var filesRemoved = !helperFilesPresent
        if helperReady || helperFilesPresent {
            let request = HelperRequest(
                operation: .uninstall,
                pid: 0,
                startTimeNanoseconds: 0,
                signal: 0
            )
            filesRemoved = (try? await send(request).errorCode) == 0
        }
        let registrationRemoved = removeRegistration()
        helperReady = false
        return HelperCleanupResult(
            filesRemoved: filesRemoved,
            registrationRemoved: registrationRemoved
        )
    }

    @MainActor
    private func authenticate(
        reason: String = "CPUAlert needs permission to terminate a Root process."
    ) async -> Bool {
        let context = LAContext()
        context.localizedCancelTitle = "Cancel"
        do {
            return try await context.evaluatePolicy(
                .deviceOwnerAuthentication,
                localizedReason: reason
            )
        } catch {
            return false
        }
    }

    private func installIfNeeded() throws {
        guard !helperReady else { return }
        var authorization: AuthorizationRef?
        let status = AuthorizationCreate(
            nil,
            nil,
            [.interactionAllowed, .extendRights, .preAuthorize],
            &authorization
        )
        guard status == errAuthorizationSuccess, let authorization else {
            throw HelperClientFailure.installationFailed
        }
        defer { AuthorizationFree(authorization, []) }

        do {
            try LegacyBlessingInstaller().install(withAuthorization: authorization)
        } catch {
            throw HelperClientFailure.installationFailed
        }
        helperReady = true
    }

    private func removeRegistration() -> Bool {
        var authorization: AuthorizationRef?
        let status = AuthorizationCreate(
            nil,
            nil,
            [.interactionAllowed, .extendRights, .preAuthorize],
            &authorization
        )
        guard status == errAuthorizationSuccess, let authorization else { return false }
        defer { AuthorizationFree(authorization, []) }

        do {
            try LegacyBlessingInstaller().removeJob(withAuthorization: authorization)
            return true
        } catch {
            return false
        }
    }

    private func send(_ request: HelperRequest) async throws -> HelperResponse {
        let connection = makeConnection()
        guard let generation = connectionGeneration else {
            throw HelperClientFailure.connectionLost
        }
        self.connection = connection
        connection.resume()

        do {
            let response = try await withCheckedThrowingContinuation { continuation in
                let gate = ReplyGate(continuation: continuation)
                guard let proxy = connection.remoteObjectProxyWithErrorHandler({ _ in
                    gate.fail(HelperClientFailure.connectionLost)
                }) as? HelperXPCProtocol else {
                    gate.fail(HelperClientFailure.invalidProxy)
                    return
                }
                proxy.perform(request) { response in
                    gate.succeed(response)
                }
            }
            connection.invalidate()
            connectionEnded(generation: generation)
            return response
        } catch {
            connection.invalidate()
            connectionEnded(generation: generation)
            throw error
        }
    }

    private func makeConnection() -> NSXPCConnection {
        let generation = UUID()
        connectionGeneration = generation
        let connection = NSXPCConnection(
            machServiceName: Self.machServiceName,
            options: .privileged
        )
        connection.setCodeSigningRequirement(Self.helperRequirement)
        connection.remoteObjectInterface = helperInterface()
        connection.interruptionHandler = { [weak self] in
            Task { await self?.connectionEnded(generation: generation) }
        }
        connection.invalidationHandler = { [weak self] in
            Task { await self?.connectionEnded(generation: generation) }
        }
        return connection
    }

    private func connectionEnded(generation: UUID) {
        guard connectionGeneration == generation else { return }
        connection = nil
        connectionGeneration = nil
    }
}

private final class ReplyGate: @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<HelperResponse, any Error>?

    init(continuation: CheckedContinuation<HelperResponse, any Error>) {
        self.continuation = continuation
    }

    func succeed(_ response: HelperResponse) {
        take()?.resume(returning: response)
    }

    func fail(_ error: any Error) {
        take()?.resume(throwing: error)
    }

    private func take() -> CheckedContinuation<HelperResponse, any Error>? {
        lock.lock()
        defer { lock.unlock() }
        defer { continuation = nil }
        return continuation
    }
}
