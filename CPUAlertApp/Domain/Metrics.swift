import Foundation

enum ResourceKind: String, CaseIterable, Codable, Sendable {
    case cpu
    case gpu
    case memory
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
    let physicalFootprintBytes: UInt64
    let isApplication: Bool

    init(
        identity: ProcessIdentity,
        name: String,
        bundleIdentifier: String?,
        ownerUID: UInt32,
        cpuUsage: Double,
        physicalFootprintBytes: UInt64 = 0,
        isApplication: Bool
    ) {
        self.identity = identity
        self.name = name
        self.bundleIdentifier = bundleIdentifier
        self.ownerUID = ownerUID
        self.cpuUsage = cpuUsage
        self.physicalFootprintBytes = physicalFootprintBytes
        self.isApplication = isApplication
    }
}

struct ProcessRankingSnapshot: Equatable, Sendable {
    let cpu: [ProcessMetric]
    let memory: [ProcessMetric]
}

struct MemoryMetric: Equatable, Sendable {
    let totalBytes: UInt64
    let usedBytes: UInt64
    let compressedBytes: UInt64

    var usage: Double {
        guard totalBytes > 0 else { return 0 }
        return max(0, min(Double(usedBytes) / Double(totalBytes), 1))
    }
}

enum MemoryFormatting {
    static func bytes(_ value: UInt64) -> String {
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useMB, .useGB, .useTB]
        formatter.countStyle = .memory
        formatter.includesUnit = true
        formatter.isAdaptive = true
        return formatter.string(fromByteCount: Int64(clamping: value))
    }

    static func usedTotal(_ metric: MemoryMetric) -> String {
        "\(bytes(metric.usedBytes)) / \(bytes(metric.totalBytes))"
    }
}

struct ThreadMetric: Identifiable, Equatable, Sendable {
    let id: UInt64
    let process: ProcessIdentity
    let name: String?
    let cpuUsage: Double
}

struct GPUGroupMemberMetric: Identifiable, Equatable, Sendable {
    var id: ProcessIdentity { identity }
    let identity: ProcessIdentity
    let name: String
    let ownerUID: UInt32
    let isApplication: Bool

    var processMetric: ProcessMetric {
        ProcessMetric(
            identity: identity,
            name: name,
            bundleIdentifier: nil,
            ownerUID: ownerUID,
            cpuUsage: 0,
            isApplication: isApplication
        )
    }
}

struct GPUGroupMetric: Identifiable, Equatable, Sendable {
    let id: UInt64
    let name: String
    let leader: ProcessIdentity?
    let members: [GPUGroupMemberMetric]
    let activityShare: Double

    func estimatedWholeMachineUsage(systemUsage: Double?) -> Double? {
        guard let systemUsage,
              systemUsage.isFinite,
              activityShare.isFinite else { return nil }
        let boundedSystemUsage = max(0, min(systemUsage, 1))
        let boundedActivityShare = max(0, min(activityShare, 1))
        return boundedSystemUsage * boundedActivityShare
    }
}

enum GPUSource: String, Equatable, Sendable {
    case ioReport
    case ioAccelerator
    case unavailable
}

struct CollectorDurations: Equatable, Sendable {
    let cpu: Duration
    let gpu: Duration
    let memory: Duration
    let rankings: Duration?

    init(
        cpu: Duration,
        gpu: Duration,
        memory: Duration = .zero,
        rankings: Duration?
    ) {
        self.cpu = cpu
        self.gpu = gpu
        self.memory = memory
        self.rankings = rankings
    }

    static let zero = CollectorDurations(
        cpu: .zero,
        gpu: .zero,
        memory: .zero,
        rankings: nil
    )
}

struct MetricsSnapshot: Equatable, Sendable {
    let cpuUsage: Double
    let gpuUsage: Double?
    let memory: MemoryMetric?
    let processes: [ProcessMetric]
    let memoryProcesses: [ProcessMetric]
    let gpuGroups: [GPUGroupMetric]
    let expandedThreads: [ThreadMetric]
    let cpuLevel: PressureLevel
    let gpuLevel: PressureLevel
    let memoryLevel: PressureLevel
    let gpuSource: GPUSource
    let sampledAt: Date
    let collectorDurations: CollectorDurations

    init(
        cpuUsage: Double,
        gpuUsage: Double?,
        memory: MemoryMetric? = nil,
        processes: [ProcessMetric],
        memoryProcesses: [ProcessMetric] = [],
        gpuGroups: [GPUGroupMetric],
        expandedThreads: [ThreadMetric],
        cpuLevel: PressureLevel,
        gpuLevel: PressureLevel,
        memoryLevel: PressureLevel = .unavailable,
        gpuSource: GPUSource,
        sampledAt: Date,
        collectorDurations: CollectorDurations = .zero
    ) {
        self.cpuUsage = cpuUsage
        self.gpuUsage = gpuUsage
        self.memory = memory
        self.processes = processes
        self.memoryProcesses = memoryProcesses
        self.gpuGroups = gpuGroups
        self.expandedThreads = expandedThreads
        self.cpuLevel = cpuLevel
        self.gpuLevel = gpuLevel
        self.memoryLevel = memoryLevel
        self.gpuSource = gpuSource
        self.sampledAt = sampledAt
        self.collectorDurations = collectorDurations
    }

    var memoryUsage: Double? { memory?.usage }

    static let empty = MetricsSnapshot(
        cpuUsage: 0,
        gpuUsage: nil,
        memory: nil,
        processes: [],
        memoryProcesses: [],
        gpuGroups: [],
        expandedThreads: [],
        cpuLevel: .green,
        gpuLevel: .unavailable,
        memoryLevel: .unavailable,
        gpuSource: .unavailable,
        sampledAt: .distantPast
    )
}
