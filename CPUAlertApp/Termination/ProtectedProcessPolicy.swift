import Darwin
import Foundation

enum ProtectedProcessPolicy {
    private static let exactNames: Set<String> = [
        "kernel_task",
        "launchd",
        "WindowServer",
        "loginwindow",
    ]

    static func isProtected(pid: Int32, name: String) -> Bool {
        pid <= 1
            || pid == getpid()
            || exactNames.contains(name)
            || name.hasPrefix("CPUAlert")
            || name.hasPrefix("com.cpualert")
    }
}
