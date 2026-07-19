import AppKit
import SwiftUI

@main
struct CPUAlertApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        Settings {
            SettingsView(
                model: appDelegate.model,
                settings: appDelegate.settings,
                loginItemService: appDelegate.loginItemService,
                helperClient: appDelegate.helperClient
            )
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private let popover = NSPopover()
    private let powerState: PowerStateMonitor
    let model: MonitorModel
    let settings: AppSettings
    let loginItemService: LoginItemService
    let helperClient: HelperClient
    private let shouldOpenAcceptanceWindow: Bool
    private let acceptanceAppearance: NSAppearance.Name?
    private var acceptancePanelWindow: NSWindow?

    override init() {
        let arguments = ProcessInfo.processInfo.arguments
        let uiTestState = UITestState(arguments: arguments)
        let benchmarkMode = BenchmarkMode(arguments: arguments)
        let engine = SamplingEngine(
            systemCPU: SystemCPUCollector(),
            processes: ProcessCPUCollector(),
            gpu: SystemGPUCollector(),
            thresholds: .defaults
        )
        let powerState = PowerStateMonitor()
        let usesIsolatedSettings = uiTestState.isEnabled || benchmarkMode != nil
        let settingsStore: any SettingsStore = usesIsolatedSettings
            ? InMemorySettingsStore()
            : UserDefaults.standard
        let settings = AppSettings(store: settingsStore, samplingEngine: engine)
        if usesIsolatedSettings {
            settings.hasCompletedFirstRun = true
            settings.showTenRows = uiTestState.showTenRows
            settings.notificationsEnabled = false
        }
        let helperClient = HelperClient()
        let terminationCoordinator = TerminationCoordinator(
            privilegedTerminator: helperClient
        )
        let loginItemService = LoginItemService()
        if !usesIsolatedSettings {
            settings.launchAtLogin = loginItemService.state == .enabled
                || loginItemService.state == .requiresApproval
        }
        self.powerState = powerState
        self.settings = settings
        self.helperClient = helperClient
        self.loginItemService = loginItemService
        model = MonitorModel(
            engine: engine,
            powerState: powerState,
            notificationService: NotificationService(),
            settings: settings,
            terminationCoordinator: terminationCoordinator,
            fixedSnapshot: uiTestState.snapshot,
            fixedTrend: uiTestState.trend
        )
        model.expandedProcess = uiTestState.expandedProcess
        if let expandedPID = benchmarkMode?.expandedPID,
           let record = ProcessIdentityReader().currentIdentity(pid: expandedPID) {
            model.expandedProcess = record.identity
        }
        model.panelIsOpen = benchmarkMode?.opensPanel ?? false
        #if DEBUG
        shouldOpenAcceptanceWindow = uiTestState.isEnabled
            || benchmarkMode?.opensPanel == true
            || arguments.contains("--open-panel")
        #else
        shouldOpenAcceptanceWindow = benchmarkMode?.opensPanel == true
        #endif
        if arguments.contains("--appearance-high-contrast-dark") {
            acceptanceAppearance = .accessibilityHighContrastDarkAqua
        } else if arguments.contains("--appearance-dark") {
            acceptanceAppearance = .darkAqua
        } else {
            acceptanceAppearance = nil
        }
        super.init()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        if let acceptanceAppearance {
            NSApplication.shared.appearance = NSAppearance(named: acceptanceAppearance)
        }
        let statusItem = NSStatusBar.system.statusItem(withLength: 56)
        guard let button = statusItem.button else { return }

        let label = MenuBarLabel(model: model)
        let hostingView = PassThroughHostingView(rootView: label)
        hostingView.translatesAutoresizingMaskIntoConstraints = false
        button.addSubview(hostingView)
        NSLayoutConstraint.activate([
            hostingView.leadingAnchor.constraint(equalTo: button.leadingAnchor, constant: 2),
            hostingView.trailingAnchor.constraint(equalTo: button.trailingAnchor, constant: -2),
            hostingView.centerYAnchor.constraint(equalTo: button.centerYAnchor),
            hostingView.heightAnchor.constraint(equalToConstant: 18),
        ])

        button.target = self
        button.action = #selector(togglePopover(_:))
        button.sendAction(on: [.leftMouseUp])
        button.toolTip = "CPUAlert"

        popover.behavior = .transient
        popover.contentSize = NSSize(width: 360, height: 500)
        popover.contentViewController = NSHostingController(
            rootView: MonitorPanel(
                model: model,
                settings: settings,
                loginItemService: loginItemService
            )
        )

        self.statusItem = statusItem

        if shouldOpenAcceptanceWindow {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in
                self?.openAcceptancePanelWindow()
            }
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        model.stop()
        powerState.stop()
    }

    @objc
    private func togglePopover(_ sender: NSStatusBarButton) {
        if popover.isShown {
            popover.performClose(sender)
        } else {
            presentPopover(relativeTo: sender)
        }
    }

    private func presentPopover(relativeTo button: NSStatusBarButton) {
        popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
    }

    private func openAcceptancePanelWindow() {
        let controller = NSHostingController(rootView: MonitorPanel(
            model: model,
            settings: settings,
            loginItemService: loginItemService
        ))
        let window = NSWindow(contentViewController: controller)
        window.title = String(localized: "app.monitor.title")
        window.styleMask = [.titled, .closable]
        window.setContentSize(NSSize(width: 360, height: 500))
        window.center()
        window.makeKeyAndOrderFront(nil)
        NSApplication.shared.activate(ignoringOtherApps: true)
        acceptancePanelWindow = window
    }
}

private final class PassThroughHostingView<Content: View>: NSHostingView<Content> {
    override func hitTest(_ point: NSPoint) -> NSView? {
        nil
    }
}
