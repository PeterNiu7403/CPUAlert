import Foundation
import Testing
@testable import CPUAlert

private actor FakeSystemCPU: SystemCPUCollecting {
    func sampleSystemCPU() async throws -> Double? { 0.72 }
}

private actor FakeProcesses: ProcessCPUCollecting {
    func sampleProcesses() async throws -> [ProcessMetric] { [] }

    func sampleThreads(for process: ProcessIdentity) async throws -> [ThreadMetric] { [] }
}

private actor FakeGPU: GPUCollecting {
    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource) {
        (0.18, .ioReport)
    }

    func sampleGroups() async throws -> [GPUGroupMetric] { [] }
}

private actor FakeMemory: SystemMemoryCollecting {
    func sampleSystemMemory() async throws -> MemoryMetric? {
        MemoryMetric(totalBytes: 1_000, usedBytes: 910, compressedBytes: 120)
    }
}

struct SamplingEngineTests {
    @Test func cyclePublishesIndependentPressureLevels() async {
        let engine = SamplingEngine(
            systemCPU: FakeSystemCPU(),
            processes: FakeProcesses(),
            gpu: FakeGPU(),
            memory: FakeMemory(),
            thresholds: .defaults
        )
        let snapshot = await engine.collectOnce(
            context: .closedGreen,
            includeRankings: true,
            now: Date(timeIntervalSince1970: 1)
        )
        #expect(snapshot.cpuUsage == 0.72)
        #expect(snapshot.gpuUsage == 0.18)
        #expect(snapshot.memory?.usedBytes == 910)
        #expect(snapshot.cpuLevel == .yellow)
        #expect(snapshot.gpuLevel == .green)
        #expect(snapshot.memoryLevel == .orange)
    }
}
