protocol SystemCPUCollecting: Sendable {
    func sampleSystemCPU() async throws -> Double?
}

protocol SystemMemoryCollecting: Sendable {
    func sampleSystemMemory() async throws -> MemoryMetric?
}

protocol ProcessCPUCollecting: Sendable {
    func sampleProcesses() async throws -> [ProcessMetric]
    func sampleProcessRankings() async throws -> ProcessRankingSnapshot
    func sampleThreads(for process: ProcessIdentity) async throws -> [ThreadMetric]
}

extension ProcessCPUCollecting {
    func sampleProcessRankings() async throws -> ProcessRankingSnapshot {
        let processes = try await sampleProcesses()
        return ProcessRankingSnapshot(
            cpu: processes,
            memory: processes.sorted {
                $0.physicalFootprintBytes == $1.physicalFootprintBytes
                    ? $0.identity.pid < $1.identity.pid
                    : $0.physicalFootprintBytes > $1.physicalFootprintBytes
            }
        )
    }
}

protocol GPUCollecting: Sendable {
    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource)
    func sampleGroups() async throws -> [GPUGroupMetric]
}
