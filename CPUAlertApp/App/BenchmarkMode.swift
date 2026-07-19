import Foundation

enum BenchmarkMode: Equatable, Sendable {
    case green
    case panelOpen
    case elevatedCPU
    case elevatedGPU
    case expandedThread(pid: Int32)

    init?(arguments: [String]) {
        if arguments.contains("--benchmark-green") {
            self = .green
        } else if arguments.contains("--benchmark-panel-open") {
            self = .panelOpen
        } else if arguments.contains("--benchmark-elevated-cpu") {
            self = .elevatedCPU
        } else if arguments.contains("--benchmark-elevated-gpu") {
            self = .elevatedGPU
        } else if arguments.contains("--benchmark-expanded-thread"),
                  let marker = arguments.firstIndex(of: "--target-pid"),
                  arguments.indices.contains(marker + 1),
                  let pid = Int32(arguments[marker + 1]),
                  pid > 0 {
            self = .expandedThread(pid: pid)
        } else {
            return nil
        }
    }

    var opensPanel: Bool {
        switch self {
        case .panelOpen, .expandedThread:
            true
        case .green, .elevatedCPU, .elevatedGPU:
            false
        }
    }

    var expandedPID: Int32? {
        guard case let .expandedThread(pid) = self else { return nil }
        return pid
    }
}
