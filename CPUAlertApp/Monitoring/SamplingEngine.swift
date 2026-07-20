import Foundation

actor SamplingEngine {
    typealias ContextProvider = @Sendable () async -> SamplingContext

    private let systemCPU: any SystemCPUCollecting
    private let processes: any ProcessCPUCollecting
    private let gpu: any GPUCollecting
    private let memory: any SystemMemoryCollecting

    private var thresholds: AlertThresholds
    private var previousCPULevel: PressureLevel = .green
    private var previousGPULevel: PressureLevel = .unavailable
    private var previousMemoryLevel: PressureLevel = .unavailable
    private var lastProcesses: [ProcessMetric] = []
    private var lastMemoryProcesses: [ProcessMetric] = []
    private var lastGPUGroups: [GPUGroupMetric] = []
    private var samplingTask: Task<Void, Never>?
    private var continuation: AsyncStream<MetricsSnapshot>.Continuation?

    init(
        systemCPU: any SystemCPUCollecting,
        processes: any ProcessCPUCollecting,
        gpu: any GPUCollecting,
        memory: any SystemMemoryCollecting,
        thresholds: AlertThresholds
    ) {
        self.systemCPU = systemCPU
        self.processes = processes
        self.gpu = gpu
        self.memory = memory
        self.thresholds = thresholds
    }

    func collectOnce(
        context: SamplingContext,
        includeRankings: Bool,
        now: Date
    ) async -> MetricsSnapshot {
        let clock = ContinuousClock()
        async let sampledCPU = collectSystemCPUTimed()
        async let sampledGPU = collectSystemGPUTimed()
        async let sampledMemory = collectSystemMemoryTimed()

        var expandedThreads: [ThreadMetric] = []
        var rankingDuration: Duration?
        if includeRankings {
            let rankingStarted = clock.now
            async let sampledProcesses = collectProcessRankings()
            async let sampledGroups = collectGPUGroups()
            async let sampledThreads = collectThreads(for: context.expandedProcess)

            if let value = await sampledProcesses {
                lastProcesses = value.cpu
                lastMemoryProcesses = value.memory
            }
            if let value = await sampledGroups {
                lastGPUGroups = value
            }
            expandedThreads = await sampledThreads ?? []
            rankingDuration = rankingStarted.duration(to: clock.now)
        }

        let cpuSample = await sampledCPU
        let gpuTimedSample = await sampledGPU
        let memoryTimedSample = await sampledMemory
        let cpuUsage = cpuSample.value
        let gpuSample = gpuTimedSample.value
        let memorySample = memoryTimedSample.value
        let cpuLevel = thresholds.level(for: cpuUsage, previous: previousCPULevel)
        let gpuLevel = thresholds.level(for: gpuSample.usage, previous: previousGPULevel)
        let memoryLevel = thresholds.level(
            for: memorySample?.usage,
            previous: previousMemoryLevel
        )
        previousCPULevel = cpuLevel
        previousGPULevel = gpuLevel
        previousMemoryLevel = memoryLevel

        return MetricsSnapshot(
            cpuUsage: cpuUsage ?? 0,
            gpuUsage: gpuSample.usage,
            memory: memorySample,
            processes: lastProcesses,
            memoryProcesses: lastMemoryProcesses,
            gpuGroups: lastGPUGroups,
            expandedThreads: expandedThreads,
            cpuLevel: cpuLevel,
            gpuLevel: gpuLevel,
            memoryLevel: memoryLevel,
            gpuSource: gpuSample.source,
            sampledAt: now,
            collectorDurations: CollectorDurations(
                cpu: cpuSample.duration,
                gpu: gpuTimedSample.duration,
                memory: memoryTimedSample.duration,
                rankings: rankingDuration
            )
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

    private func collectSystemCPUTimed() async -> (value: Double?, duration: Duration) {
        let clock = ContinuousClock()
        let started = clock.now
        let value = await collectSystemCPU()
        return (value, started.duration(to: clock.now))
    }

    private func collectSystemGPU() async -> (usage: Double?, source: GPUSource) {
        do {
            return try await gpu.sampleSystemGPU()
        } catch {
            return (nil, .unavailable)
        }
    }

    private func collectSystemGPUTimed() async -> (
        value: (usage: Double?, source: GPUSource),
        duration: Duration
    ) {
        let clock = ContinuousClock()
        let started = clock.now
        let value = await collectSystemGPU()
        return (value, started.duration(to: clock.now))
    }

    private func collectProcessRankings() async -> ProcessRankingSnapshot? {
        do {
            return try await processes.sampleProcessRankings()
        } catch {
            return nil
        }
    }

    private func collectSystemMemory() async -> MemoryMetric? {
        do {
            return try await memory.sampleSystemMemory()
        } catch {
            return nil
        }
    }

    private func collectSystemMemoryTimed() async -> (
        value: MemoryMetric?,
        duration: Duration
    ) {
        let clock = ContinuousClock()
        let started = clock.now
        let value = await collectSystemMemory()
        return (value, started.duration(to: clock.now))
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
