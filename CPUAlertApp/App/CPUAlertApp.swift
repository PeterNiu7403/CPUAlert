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
    #if DEBUG
    private var debugPanelWindow: NSWindow?
    #endif

    override init() {
        let uiTestState = UITestState(arguments: ProcessInfo.processInfo.arguments)
        let engine = SamplingEngine(
            systemCPU: SystemCPUCollector(),
            processes: ProcessCPUCollector(),
            gpu: SystemGPUCollector(),
            thresholds: .defaults
        )
        let powerState = PowerStateMonitor()
        let settingsStore: any SettingsStore = uiTestState.isEnabled
            ? InMemorySettingsStore()
            : UserDefaults.standard
        let settings = AppSettings(store: settingsStore, samplingEngine: engine)
        if uiTestState.isEnabled {
            settings.hasCompletedFirstRun = true
            settings.showTenRows = uiTestState.showTenRows
            settings.notificationsEnabled = false
        }
        let helperClient = HelperClient()
        let terminationCoordinator = TerminationCoordinator(
            privilegedTerminator: helperClient
        )
        let loginItemService = LoginItemService()
        if !uiTestState.isEnabled {
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
        super.init()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
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

        #if DEBUG
        if ProcessInfo.processInfo.arguments.contains("--open-panel")
            || ProcessInfo.processInfo.arguments.contains("--ui-testing") {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in
                self?.openDebugPanelWindow()
            }
        }
        #endif
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

    #if DEBUG
    private func openDebugPanelWindow() {
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
        debugPanelWindow = window
    }
    #endif
}

private final class PassThroughHostingView<Content: View>: NSHostingView<Content> {
    override func hitTest(_ point: NSPoint) -> NSView? {
        nil
    }
}
