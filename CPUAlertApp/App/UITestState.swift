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
                ownerUID: 501,
                cpuUsage: usage,
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
            processes: processes,
            gpuGroups: gpuUnavailable ? [] : groups,
            expandedThreads: threads,
            cpuLevel: isRed ? .red : .green,
            gpuLevel: gpuUnavailable ? .unavailable : .green,
            gpuSource: gpuUnavailable ? .unavailable : .ioReport,
            sampledAt: sampledAt,
            collectorDurations: CollectorDurations(
                cpu: .milliseconds(2),
                gpu: .milliseconds(3),
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
                processes: processes,
                gpuGroups: value.gpuGroups,
                expandedThreads: threads,
                cpuLevel: value.cpuLevel,
                gpuLevel: value.gpuLevel,
                gpuSource: value.gpuSource,
                sampledAt: sampledAt.addingTimeInterval(Double(offset) * 5),
                collectorDurations: value.collectorDurations
            ))
        }
        trend = trendValues
    }
}
