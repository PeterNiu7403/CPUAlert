import Foundation

enum ResourceKind: String, CaseIterable, Codable, Sendable {
    case cpu
    case gpu
}

enum PressureLevel: Int, Codable, Comparable, Sendable {
    case unavailable = -1
    case green = 0
    case yellow = 1
    case orange = 2
    case red = 3

    static func < (lhs: Self, rhs: Self) -> Bool {
        lhs.rawValue < rhs.rawValue
    }
}

struct ProcessIdentity: Hashable, Codable, Sendable {
    let pid: Int32
    let startTimeNanoseconds: UInt64
}

struct ProcessMetric: Identifiable, Equatable, Sendable {
    var id: ProcessIdentity { identity }
    let identity: ProcessIdentity
    let name: String
    let bundleIdentifier: String?
    let ownerUID: UInt32
    let cpuUsage: Double
    let isApplication: Bool
}

struct ThreadMetric: Identifiable, Equatable, Sendable {
    let id: UInt64
    let process: ProcessIdentity
    let name: String?
    let cpuUsage: Double
}

struct GPUGroupMetric: Identifiable, Equatable, Sendable {
    let id: UInt64
    let name: String
    let leader: ProcessIdentity?
    let members: [ProcessIdentity]
    let activityShare: Double
}

enum GPUSource: String, Equatable, Sendable {
    case ioReport
    case ioAccelerator
    case unavailable
}

struct MetricsSnapshot: Equatable, Sendable {
    let cpuUsage: Double
    let gpuUsage: Double?
    let processes: [ProcessMetric]
    let gpuGroups: [GPUGroupMetric]
    let expandedThreads: [ThreadMetric]
    let cpuLevel: PressureLevel
    let gpuLevel: PressureLevel
    let gpuSource: GPUSource
    let sampledAt: Date

    static let empty = MetricsSnapshot(
        cpuUsage: 0,
        gpuUsage: nil,
        processes: [],
        gpuGroups: [],
        expandedThreads: [],
        cpuLevel: .green,
        gpuLevel: .unavailable,
        gpuSource: .unavailable,
        sampledAt: .distantPast
    )
}
