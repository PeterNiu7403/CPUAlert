protocol SystemCPUCollecting: Sendable {
    func sampleSystemCPU() async throws -> Double?
}

protocol ProcessCPUCollecting: Sendable {
    func sampleProcesses() async throws -> [ProcessMetric]
    func sampleThreads(for process: ProcessIdentity) async throws -> [ThreadMetric]
}

protocol GPUCollecting: Sendable {
    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource)
    func sampleGroups() async throws -> [GPUGroupMetric]
}
