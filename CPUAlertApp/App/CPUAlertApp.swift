import AppKit
import SwiftUI

@main
struct CPUAlertApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        Settings {
            Text("CPUAlert Settings")
                .frame(width: 420, height: 260)
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem?
    private let popover = NSPopover()

    func applicationDidFinishLaunching(_ notification: Notification) {
        let statusItem = NSStatusBar.system.statusItem(withLength: 56)
        guard let button = statusItem.button else { return }

        let label = MenuBarLabel(
            cpuUsage: 0.42,
            gpuUsage: 0.18,
            cpuColor: .green,
            gpuColor: .green
        )
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
        popover.contentSize = NSSize(width: 260, height: 96)
        popover.contentViewController = NSHostingController(
            rootView: Text("CPUAlert rendering spike")
                .padding()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        )

        self.statusItem = statusItem
    }

    @objc
    private func togglePopover(_ sender: NSStatusBarButton) {
        if popover.isShown {
            popover.performClose(sender)
        } else {
            popover.show(relativeTo: sender.bounds, of: sender, preferredEdge: .minY)
        }
    }
}

private final class PassThroughHostingView<Content: View>: NSHostingView<Content> {
    override func hitTest(_ point: NSPoint) -> NSView? {
        nil
    }
}
