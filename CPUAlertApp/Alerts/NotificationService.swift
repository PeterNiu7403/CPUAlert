import Foundation
import UserNotifications

actor NotificationService {
    private enum AuthorizationState {
        case unknown
        case authorized
        case denied
    }

    private let center: UNUserNotificationCenter
    private var authorizationState: AuthorizationState = .unknown
    private var pending: [ResourceKind: AlertTrigger] = [:]
    private var pendingSnapshot: MetricsSnapshot?
    private var mergeTask: Task<Void, Never>?

    init(center: UNUserNotificationCenter = .current()) {
        self.center = center
    }

    func requestAuthorization() async -> Bool {
        switch authorizationState {
        case .authorized:
            return true
        case .denied:
            return false
        case .unknown:
            break
        }

        let settings = await center.notificationSettings()
        switch settings.authorizationStatus {
        case .authorized, .provisional, .ephemeral:
            authorizationState = .authorized
            return true
        case .denied:
            authorizationState = .denied
            return false
        case .notDetermined:
            do {
                let granted = try await center.requestAuthorization(options: [.alert, .sound])
                authorizationState = granted ? .authorized : .denied
                return granted
            } catch {
                authorizationState = .denied
                return false
            }
        @unknown default:
            authorizationState = .denied
            return false
        }
    }

    func enqueue(_ triggers: [AlertTrigger], snapshot: MetricsSnapshot) async {
        guard !triggers.isEmpty, await requestAuthorization() else { return }

        for trigger in triggers {
            if let existing = pending[trigger.resource], existing.level > trigger.level {
                continue
            }
            pending[trigger.resource] = trigger
        }
        pendingSnapshot = snapshot

        guard mergeTask == nil else { return }
        mergeTask = Task { [weak self] in
            do {
                try await Task.sleep(for: .seconds(2))
            } catch {
                return
            }
            await self?.deliverPending()
        }
    }

    private func deliverPending() async {
        let triggers = pending
        let snapshot = pendingSnapshot
        pending.removeAll(keepingCapacity: true)
        pendingSnapshot = nil
        mergeTask = nil

        guard let snapshot, !triggers.isEmpty else { return }
        let content = UNMutableNotificationContent()
        content.sound = .default

        if triggers[.cpu] != nil, triggers[.gpu] != nil {
            content.title = "CPUAlert: High system load"
            var details = [
                "CPU \(percent(snapshot.cpuUsage))",
                "GPU \(snapshot.gpuUsage.map(percent) ?? "—")",
            ]
            if let process = snapshot.processes.first?.name {
                details.append("Top process: \(process)")
            }
            if let group = snapshot.gpuGroups.first?.name {
                details.append("Top GPU group: \(group)")
            }
            content.body = details.joined(separator: " · ")
        } else if let trigger = triggers[.cpu] {
            content.title = "CPUAlert: CPU \(levelName(trigger.level))"
            var body = "CPU \(percent(snapshot.cpuUsage))"
            if let process = snapshot.processes.first?.name {
                body += " · Top process: \(process)"
            }
            content.body = body
        } else if let trigger = triggers[.gpu] {
            content.title = "CPUAlert: GPU \(levelName(trigger.level))"
            var body = "GPU \(snapshot.gpuUsage.map(percent) ?? "—")"
            if let group = snapshot.gpuGroups.first?.name {
                body += " · Top group: \(group)"
            }
            content.body = body
        }

        let request = UNNotificationRequest(
            identifier: UUID().uuidString,
            content: content,
            trigger: nil
        )
        try? await center.add(request)
    }

    private func percent(_ usage: Double) -> String {
        "\(Int((max(0, min(usage, 1)) * 100).rounded()))%"
    }

    private func levelName(_ level: PressureLevel) -> String {
        switch level {
        case .yellow: "elevated"
        case .orange: "high"
        case .red: "critical"
        case .green: "normal"
        case .unavailable: "unavailable"
        }
    }
}
