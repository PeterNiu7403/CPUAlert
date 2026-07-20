import Darwin
import Foundation

struct UITestState {
    let isEnabled: Bool
    let snapshot: MetricsSnapshot?
    let trend: [MetricsSnapshot]
    let showTenRows: Bool
    let expandedProcess: ProcessIdentity?

    init(arguments: [String]) {
        isEnabled = arguments.contains("--ui-testing")
        guard isEnabled else {
            snapshot = nil
            trend = []
            showTenRows = false
            expandedProcess = nil
            return
        }

        showTenRows = arguments.contains("--rows-10")
        if arguments.contains("--live-sampling") {
            snapshot = nil
            trend = []
            expandedProcess = nil
            return
        }
        let firstIdentity = ProcessIdentity(pid: 4_201, startTimeNanoseconds: 1)
        expandedProcess = arguments.contains("--expanded-threads") ? firstIdentity : nil
        let isRed = arguments.contains("--state-red-cpu")
        let gpuUnavailable = arguments.contains("--gpu-unavailable")
        let sampledAt = Date(timeIntervalSince1970: 1_700_000_000)

        var processes: [ProcessMetric] = []
        for index in 1...10 {
            let identity = ProcessIdentity(
                pid: Int32(4_200 + index),
                startTimeNanoseconds: UInt64(index)
            )
            let usage = max(0.01, (isRed ? 0.38 : 0.18) - Double(index) * 0.01)
            processes.append(ProcessMetric(
                identity: identity,
                name: index == 1 ? "CPUStress" : "Fixture \(index)",
                bundleIdentifier: "com.cpualert.fixture.\(index)",
                ownerUID: getuid(),
                cpuUsage: usage,
                physicalFootprintBytes: UInt64(12 - index) * 512 * 1_024 * 1_024,
                isApplication: true
            ))
        }
        var groups: [GPUGroupMetric] = []
        for index in 1...10 {
            let process = processes[index - 1]
            groups.append(GPUGroupMetric(
                id: UInt64(index),
                name: index == 1 ? "Metal Fixture" : "GPU Group \(index)",
                leader: process.identity,
                members: [GPUGroupMemberMetric(
                    identity: process.identity,
                    name: process.name,
                    ownerUID: process.ownerUID,
                    isApplication: process.isApplication
                )],
                activityShare: max(0.01, 0.28 - Double(index) * 0.015)
            ))
        }
        let threads = [
            ThreadMetric(id: 101, process: firstIdentity, name: "render-loop", cpuUsage: 0.08),
            ThreadMetric(id: 102, process: firstIdentity, name: "worker", cpuUsage: 0.04),
        ]

        let value = MetricsSnapshot(
            cpuUsage: isRed ? 0.97 : 0.42,
            gpuUsage: gpuUnavailable ? nil : 0.36,
            memory: MemoryMetric(
                totalBytes: 16 * 1_024 * 1_024 * 1_024,
                usedBytes: 10 * 1_024 * 1_024 * 1_024,
                compressedBytes: 1_024 * 1_024 * 1_024
            ),
            processes: processes,
            memoryProcesses: processes.sorted {
                $0.physicalFootprintBytes > $1.physicalFootprintBytes
            },
            gpuGroups: gpuUnavailable ? [] : groups,
            expandedThreads: threads,
            cpuLevel: isRed ? .red : .green,
            gpuLevel: gpuUnavailable ? .unavailable : .green,
            memoryLevel: .green,
            gpuSource: gpuUnavailable ? .unavailable : .ioReport,
            sampledAt: sampledAt,
            collectorDurations: CollectorDurations(
                cpu: .milliseconds(2),
                gpu: .milliseconds(3),
                memory: .milliseconds(1),
                rankings: .milliseconds(5)
            )
        )
        snapshot = value
        var trendValues: [MetricsSnapshot] = []
        for offset in 0..<12 {
            let cpuUsage = max(0, value.cpuUsage - Double(11 - offset) * 0.015)
            let gpuUsage = value.gpuUsage.map {
                max(0, $0 - Double(11 - offset) * 0.01)
            }
            trendValues.append(MetricsSnapshot(
                cpuUsage: cpuUsage,
                gpuUsage: gpuUsage,
                memory: MemoryMetric(
                    totalBytes: value.memory?.totalBytes ?? 0,
                    usedBytes: UInt64(
                        Double(value.memory?.usedBytes ?? 0)
                            * (0.84 + Double(offset) * 0.014)
                    ),
                    compressedBytes: value.memory?.compressedBytes ?? 0
                ),
                processes: processes,
                memoryProcesses: value.memoryProcesses,
                gpuGroups: value.gpuGroups,
                expandedThreads: threads,
                cpuLevel: value.cpuLevel,
                gpuLevel: value.gpuLevel,
                memoryLevel: value.memoryLevel,
                gpuSource: value.gpuSource,
                sampledAt: sampledAt.addingTimeInterval(Double(offset) * 5),
                collectorDurations: value.collectorDurations
            ))
        }
        trend = trendValues
    }
}
