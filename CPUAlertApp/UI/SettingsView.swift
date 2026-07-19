import SwiftUI

struct SettingsView: View {
    @Bindable var model: MonitorModel
    @Bindable var settings: AppSettings
    @Bindable var loginItemService: LoginItemService
    let helperClient: HelperClient

    @State private var yellow: Double
    @State private var orange: Double
    @State private var red: Double
    @State private var thresholdError = false
    @State private var notificationRequestInProgress = false
    @State private var notificationFeedbackKey: LocalizedStringKey?
    @State private var helperInstalled = false
    @State private var helperActionInProgress = false
    @State private var helperFeedbackKey: LocalizedStringKey?

    init(
        model: MonitorModel,
        settings: AppSettings,
        loginItemService: LoginItemService,
        helperClient: HelperClient
    ) {
        self.model = model
        self.settings = settings
        self.loginItemService = loginItemService
        self.helperClient = helperClient
        _yellow = State(initialValue: settings.thresholds.yellow)
        _orange = State(initialValue: settings.thresholds.orange)
        _red = State(initialValue: settings.thresholds.red)
    }

    var body: some View {
        TabView {
            generalSection
                .tabItem { Label("settings.general", systemImage: "gearshape") }
            alertsSection
                .tabItem { Label("settings.alerts", systemImage: "bell.badge") }
            privilegeSection
                .tabItem { Label("settings.privilege", systemImage: "lock.shield") }
            diagnosticsSection
                .tabItem { Label("settings.diagnostics", systemImage: "stethoscope") }
        }
        .padding(14)
        .frame(width: 420, height: 390)
        .task {
            loginItemService.refresh()
            helperInstalled = await helperClient.isInstalled()
        }
    }

    private var generalSection: some View {
        Form {
            Toggle("settings.general.launchAtLogin", isOn: launchAtLoginBinding)
                .accessibilityIdentifier("settings-launch-at-login")
            LabeledContent("settings.general.loginStatus") {
                Text(loginItemStatusKey)
            }
            if loginItemService.state == .requiresApproval {
                Button("loginItem.openSettings") {
                    loginItemService.openSystemSettings()
                }
            }
            if let lastError = loginItemService.lastError {
                Text(lastError)
                    .font(.caption)
                    .foregroundStyle(.red)
            }
            Picker("settings.general.rows", selection: $model.showTenRows) {
                Text("settings.general.rows5").tag(false)
                Text("settings.general.rows10").tag(true)
            }
            .pickerStyle(.segmented)
            .accessibilityIdentifier("settings-visible-rows")
            Button("settings.general.resetFirstRun") {
                settings.hasCompletedFirstRun = false
            }
            .accessibilityIdentifier("settings-reset-first-run")
        }
        .formStyle(.grouped)
    }

    private var alertsSection: some View {
        Form {
            thresholdRow(
                key: "settings.alerts.yellow",
                value: $yellow,
                range: 0.50...max(0.50, orange - 0.05)
            )
            thresholdRow(
                key: "settings.alerts.orange",
                value: $orange,
                range: min(0.95, yellow + 0.05)...max(min(0.95, yellow + 0.05), red - 0.05)
            )
            thresholdRow(
                key: "settings.alerts.red",
                value: $red,
                range: min(1, orange + 0.05)...1
            )
            if thresholdError {
                Text("settings.alerts.validation")
                    .foregroundStyle(.red)
                    .font(.caption)
            }
            Toggle("settings.alerts.notifications", isOn: notificationsBinding)
                .disabled(notificationRequestInProgress)
            if let notificationFeedbackKey {
                Text(notificationFeedbackKey)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Button("settings.alerts.reset") {
                let defaults = AlertThresholds.defaults
                yellow = defaults.yellow
                orange = defaults.orange
                red = defaults.red
                applyThresholds()
            }
        }
        .formStyle(.grouped)
    }

    private var privilegeSection: some View {
        Form {
            LabeledContent("settings.privilege.status") {
                Text(LocalizedStringKey(
                    helperInstalled ? "settings.status.installed" : "settings.status.notInstalled"
                ))
            }
            Text("settings.privilege.legacyWarning")
                .font(.caption)
                .foregroundStyle(.secondary)
            Button {
                performHelperAction()
            } label: {
                Text(LocalizedStringKey(
                    helperInstalled ? "settings.privilege.remove" : "settings.privilege.install"
                ))
            }
            .disabled(helperActionInProgress)
            if let helperFeedbackKey {
                Text(helperFeedbackKey)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
    }

    private var diagnosticsSection: some View {
        Form {
            LabeledContent("settings.diagnostics.systemCadence") {
                Text(duration(model.currentCadence.system))
            }
            LabeledContent("settings.diagnostics.rankingCadence") {
                Text(duration(model.currentCadence.ranking))
            }
            LabeledContent("settings.diagnostics.gpuSource") {
                Text(gpuSourceKey)
            }
            LabeledContent("settings.diagnostics.cpuDuration") {
                Text(duration(model.snapshot.collectorDurations.cpu))
            }
            LabeledContent("settings.diagnostics.gpuDuration") {
                Text(duration(model.snapshot.collectorDurations.gpu))
            }
            LabeledContent("settings.diagnostics.rankingDuration") {
                Text(model.snapshot.collectorDurations.rankings.map(duration) ?? String(localized: "value.unavailable"))
            }
            Text("settings.diagnostics.privacy")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .formStyle(.grouped)
    }

    private var launchAtLoginBinding: Binding<Bool> {
        Binding(
            get: { settings.launchAtLogin },
            set: { enabled in
                let accepted = loginItemService.setEnabled(enabled)
                settings.launchAtLogin = accepted ? enabled : loginItemService.state == .enabled
            }
        )
    }

    private var notificationsBinding: Binding<Bool> {
        Binding(
            get: { settings.notificationsEnabled },
            set: { enabled in
                notificationFeedbackKey = nil
                if !enabled {
                    settings.notificationsEnabled = false
                    return
                }
                notificationRequestInProgress = true
                Task {
                    let granted = await model.requestNotificationAuthorization()
                    notificationFeedbackKey = granted
                        ? "onboarding.notifications.allowed"
                        : "onboarding.notifications.denied"
                    notificationRequestInProgress = false
                }
            }
        )
    }

    private var loginItemStatusKey: LocalizedStringKey {
        switch loginItemService.state {
        case .enabled: "settings.loginItem.enabled"
        case .requiresApproval: "settings.loginItem.requiresApproval"
        case .notRegistered: "settings.loginItem.notRegistered"
        case .notFound: "settings.loginItem.notFound"
        }
    }

    private func thresholdRow(
        key: LocalizedStringKey,
        value: Binding<Double>,
        range: ClosedRange<Double>
    ) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            LabeledContent(key) {
                Text(value.wrappedValue, format: .percent.precision(.fractionLength(0)))
                    .monospacedDigit()
            }
            Slider(value: value, in: range, step: 0.05)
                .onChange(of: value.wrappedValue) { _, _ in applyThresholds() }
        }
    }

    private func applyThresholds() {
        let quantized = { (value: Double) in (value * 100).rounded() / 100 }
        thresholdError = !settings.setThresholds(
            yellow: quantized(yellow),
            orange: quantized(orange),
            red: quantized(red)
        )
    }

    private func performHelperAction() {
        helperActionInProgress = true
        Task {
            if helperInstalled {
                let result = await helperClient.uninstall()
                helperFeedbackKey = result.filesRemoved && result.registrationRemoved
                    ? "settings.privilege.removed"
                    : "settings.privilege.partialCleanup"
            } else {
                let installed = await helperClient.install()
                helperFeedbackKey = installed
                    ? "settings.privilege.installed"
                    : "settings.privilege.installFailed"
            }
            helperInstalled = await helperClient.isInstalled()
            helperActionInProgress = false
        }
    }

    private var gpuSourceKey: LocalizedStringKey {
        switch model.snapshot.gpuSource {
        case .ioReport: "settings.diagnostics.gpu.ioReport"
        case .ioAccelerator: "settings.diagnostics.gpu.ioAccelerator"
        case .unavailable: "value.unavailable"
        }
    }

    private func duration(_ value: Duration) -> String {
        let components = value.components
        let milliseconds = Double(components.seconds) * 1_000
            + Double(components.attoseconds) / 1_000_000_000_000_000
        return String(
            format: String(localized: "value.milliseconds.format"),
            locale: .current,
            milliseconds
        )
    }
}
