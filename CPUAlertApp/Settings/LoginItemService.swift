import Observation
import ServiceManagement

enum LoginItemDisplayState: Equatable, Sendable {
    case enabled
    case requiresApproval
    case notRegistered
    case notFound
}

@MainActor
@Observable
final class LoginItemService {
    private(set) var state: LoginItemDisplayState
    private(set) var lastError: String?

    @ObservationIgnored private let service: SMAppService

    init(service: SMAppService = .mainApp) {
        self.service = service
        state = Self.map(service.status)
    }

    @discardableResult
    func setEnabled(_ enabled: Bool) -> Bool {
        lastError = nil
        if enabled, service.status == .requiresApproval {
            refresh()
            return false
        }

        do {
            if enabled {
                if service.status != .enabled {
                    try service.register()
                }
            } else if service.status != .notRegistered {
                try service.unregister()
            }
            refresh()
            return enabled ? state == .enabled || state == .requiresApproval : state == .notRegistered
        } catch {
            lastError = error.localizedDescription
            refresh()
            return false
        }
    }

    func refresh() {
        state = Self.map(service.status)
    }

    func openSystemSettings() {
        SMAppService.openSystemSettingsLoginItems()
    }

    private static func map(_ status: SMAppService.Status) -> LoginItemDisplayState {
        switch status {
        case .enabled: .enabled
        case .requiresApproval: .requiresApproval
        case .notRegistered: .notRegistered
        case .notFound: .notFound
        @unknown default: .notFound
        }
    }
}
