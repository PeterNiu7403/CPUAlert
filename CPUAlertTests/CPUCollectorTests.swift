import Testing
@testable import CPUAlert

struct CPUCollectorTests {
    @Test func systemTicksProduceWholeMachineUsage() {
        let previous = SystemCPUTicks(user: 100, system: 50, idle: 850, nice: 0)
        let current = SystemCPUTicks(user: 130, system: 70, idle: 900, nice: 0)
        #expect(SystemCPUCollector.usage(previous: previous, current: current) == 0.5)
    }

    @Test func processTimeIsNormalizedAcrossLogicalCPUs() {
        let usage = ProcessCPUCollector.normalizedUsage(
            previousNanoseconds: 1_000_000_000,
            currentNanoseconds: 3_000_000_000,
            elapsedNanoseconds: 1_000_000_000,
            logicalCPUCount: 10
        )
        #expect(usage == 0.2)
    }

    @Test func counterRegressionDropsTheSample() {
        #expect(ProcessCPUCollector.normalizedUsage(
            previousNanoseconds: 3,
            currentNanoseconds: 2,
            elapsedNanoseconds: 1,
            logicalCPUCount: 10
        ) == nil)
    }

    @Test func liveCollectorsStayWithinWholeMachineBounds() async throws {
        let systemCollector = SystemCPUCollector()
        let processCollector = ProcessCPUCollector()

        _ = try await systemCollector.sampleSystemCPU()
        _ = try await processCollector.sampleProcesses()
        try await Task.sleep(for: .milliseconds(100))

        let systemUsage = try await systemCollector.sampleSystemCPU()
        let processes = try await processCollector.sampleProcesses()
        #expect(systemUsage.map { (0...1).contains($0) } ?? true)
        #expect(processes.allSatisfy { $0.cpuUsage.isFinite && (0...1).contains($0.cpuUsage) })

        if let process = processes.first {
            _ = try await processCollector.sampleThreads(for: process.identity)
            try await Task.sleep(for: .milliseconds(50))
            let threads = try await processCollector.sampleThreads(for: process.identity)
            #expect(threads.allSatisfy { $0.cpuUsage.isFinite && (0...1).contains($0.cpuUsage) })
        }
    }
}
