import SwiftUI

struct FirstRunView: View {
    @Bindable var settings: AppSettings
    @Bindable var loginItemService: LoginItemService
    let model: MonitorModel

    @State private var isRequestingNotifications = false
    @State private var feedbackKey: LocalizedStringKey?

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("onboarding.title")
                        .font(.headline)
                    Text("onboarding.summary")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Image(systemName: "gauge.with.dots.needle.50percent")
                    .foregroundStyle(.tint)
                    .accessibilityHidden(true)
            }

            HStack {
                Button("onboarding.notifications") {
                    requestNotifications()
                }
                .disabled(isRequestingNotifications || settings.notificationsEnabled)

                Button("onboarding.loginItem") {
                    enableLoginItem()
                }
                .disabled(settings.launchAtLogin)
            }

            if loginItemService.state == .requiresApproval {
                Button("loginItem.openSettings") {
                    loginItemService.openSystemSettings()
                }
                .buttonStyle(.link)
            }

            if let feedbackKey {
                Text(feedbackKey)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            HStack {
                Button("onboarding.notNow") {
                    settings.hasCompletedFirstRun = true
                }
                .buttonStyle(.plain)
                Spacer()
                Button("onboarding.continue") {
                    settings.hasCompletedFirstRun = true
                }
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(12)
        .background(.quaternary.opacity(0.7), in: RoundedRectangle(cornerRadius: 10))
        .accessibilityElement(children: .contain)
    }

    private func requestNotifications() {
        isRequestingNotifications = true
        Task {
            let granted = await model.requestNotificationAuthorization()
            feedbackKey = granted
                ? "onboarding.notifications.allowed"
                : "onboarding.notifications.denied"
            isRequestingNotifications = false
        }
    }

    private func enableLoginItem() {
        let accepted = loginItemService.setEnabled(true)
        settings.launchAtLogin = accepted || loginItemService.state == .requiresApproval
        feedbackKey = accepted
            ? "onboarding.loginItem.enabled"
            : "onboarding.loginItem.needsApproval"
    }
}
