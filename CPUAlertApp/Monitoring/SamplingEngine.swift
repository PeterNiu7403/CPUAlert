import Foundation

actor SamplingEngine {
    typealias ContextProvider = @Sendable () async -> SamplingContext

    private let systemCPU: any SystemCPUCollecting
    private let processes: any ProcessCPUCollecting
    private let gpu: any GPUCollecting

    private var thresholds: AlertThresholds
    private var previousCPULevel: PressureLevel = .green
    private var previousGPULevel: PressureLevel = .unavailable
    private var lastProcesses: [ProcessMetric] = []
    private var lastGPUGroups: [GPUGroupMetric] = []
    private var samplingTask: Task<Void, Never>?
    private var continuation: AsyncStream<MetricsSnapshot>.Continuation?

    init(
        systemCPU: any SystemCPUCollecting,
        processes: any ProcessCPUCollecting,
        gpu: any GPUCollecting,
        thresholds: AlertThresholds
    ) {
        self.systemCPU = systemCPU
        self.processes = processes
        self.gpu = gpu
        self.thresholds = thresholds
    }

    func collectOnce(
        context: SamplingContext,
        includeRankings: Bool,
        now: Date
    ) async -> MetricsSnapshot {
        async let sampledCPU = collectSystemCPU()
        async let sampledGPU = collectSystemGPU()

        var expandedThreads: [ThreadMetric] = []
        if includeRankings {
            async let sampledProcesses = collectProcesses()
            async let sampledGroups = collectGPUGroups()
            async let sampledThreads = collectThreads(for: context.expandedProcess)

            if let value = await sampledProcesses {
                lastProcesses = value
            }
            if let value = await sampledGroups {
                lastGPUGroups = value
            }
            expandedThreads = await sampledThreads ?? []
        }

        let cpuUsage = await sampledCPU
        let gpuSample = await sampledGPU
        let cpuLevel = thresholds.level(for: cpuUsage, previous: previousCPULevel)
        let gpuLevel = thresholds.level(for: gpuSample.usage, previous: previousGPULevel)
        previousCPULevel = cpuLevel
        previousGPULevel = gpuLevel

        return MetricsSnapshot(
            cpuUsage: cpuUsage ?? 0,
            gpuUsage: gpuSample.usage,
            processes: lastProcesses,
            gpuGroups: lastGPUGroups,
            expandedThreads: expandedThreads,
            cpuLevel: cpuLevel,
            gpuLevel: gpuLevel,
            gpuSource: gpuSample.source,
            sampledAt: now
        )
    }

    func updateThresholds(_ thresholds: AlertThresholds) {
        self.thresholds = thresholds
    }

    func snapshots(
        context: @escaping ContextProvider
    ) -> AsyncStream<MetricsSnapshot> {
        let pair = AsyncStream<MetricsSnapshot>.makeStream()
        guard samplingTask == nil else {
            pair.continuation.finish()
            return pair.stream
        }

        continuation = pair.continuation
        pair.continuation.onTermination = { [weak self] _ in
            Task { await self?.stop() }
        }
        samplingTask = Task { [weak self] in
            await self?.run(context: context)
        }
        return pair.stream
    }

    func stop() {
        let activeTask = samplingTask
        samplingTask = nil
        activeTask?.cancel()

        let activeContinuation = continuation
        continuation = nil
        activeContinuation?.finish()
    }

    private func run(context: @escaping ContextProvider) async {
        let clock = ContinuousClock()
        var rankingDeadline = clock.now

        while !Task.isCancelled {
            let currentContext = await context()
            let cadence = SamplingPolicy.cadence(for: currentContext)
            let monotonicNow = clock.now
            let includeRankings = monotonicNow >= rankingDeadline
            let snapshot = await collectOnce(
                context: currentContext,
                includeRankings: includeRankings,
                now: Date()
            )
            continuation?.yield(snapshot)
            if includeRankings {
                rankingDeadline = monotonicNow.advanced(by: cadence.ranking)
            }

            do {
                try await clock.sleep(for: cadence.system)
            } catch {
                break
            }
        }

        if samplingTask != nil {
            let activeContinuation = continuation
            samplingTask = nil
            continuation = nil
            activeContinuation?.finish()
        }
    }

    private func collectSystemCPU() async -> Double? {
        do {
            return try await systemCPU.sampleSystemCPU()
        } catch {
            return nil
        }
    }

    private func collectSystemGPU() async -> (usage: Double?, source: GPUSource) {
        do {
            return try await gpu.sampleSystemGPU()
        } catch {
            return (nil, .unavailable)
        }
    }

    private func collectProcesses() async -> [ProcessMetric]? {
        do {
            return try await processes.sampleProcesses()
        } catch {
            return nil
        }
    }

    private func collectGPUGroups() async -> [GPUGroupMetric]? {
        do {
            return try await gpu.sampleGroups()
        } catch {
            return nil
        }
    }

    private func collectThreads(for process: ProcessIdentity?) async -> [ThreadMetric]? {
        guard let process else { return [] }
        do {
            return try await processes.sampleThreads(for: process)
        } catch {
            return nil
        }
    }
}
