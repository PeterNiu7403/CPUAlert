import Observation
import SwiftUI

@MainActor
@Observable
final class MonitorModel {
    private(set) var snapshot = MetricsSnapshot.empty
    var selectedResource: ResourceKind = .cpu
    var panelIsOpen = false
    var showTenRows = false
    var expandedProcess: ProcessIdentity?
    private(set) var trend: [MetricsSnapshot] = []

    @ObservationIgnored private let engine: SamplingEngine
    @ObservationIgnored private let powerState: PowerStateMonitor
    @ObservationIgnored private let notificationService: NotificationService
    @ObservationIgnored private let clock = ContinuousClock()
    @ObservationIgnored private let startedAt: ContinuousClock.Instant
    @ObservationIgnored private var observationTask: Task<Void, Never>?
    @ObservationIgnored private var alertEngine = AlertEngine()

    init(
        engine: SamplingEngine,
        powerState: PowerStateMonitor,
        notificationService: NotificationService
    ) {
        self.engine = engine
        self.powerState = powerState
        self.notificationService = notificationService
        startedAt = clock.now
    }

    func start() {
        guard observationTask == nil else { return }
        observationTask = Task { [weak self] in
            guard let self else { return }
            let stream = await engine.snapshots { [weak self] in
                await self?.samplingContext ?? .closedGreen
            }
            for await value in stream {
                guard !Task.isCancelled else { break }
                snapshot = value
                let elapsed = clock.now - startedAt
                var triggers = alertEngine.evaluate(
                    resource: .cpu,
                    level: value.cpuLevel,
                    elapsed: elapsed
                )
                triggers += alertEngine.evaluate(
                    resource: .gpu,
                    level: value.gpuLevel,
                    elapsed: elapsed
                )
                if !triggers.isEmpty {
                    await notificationService.enqueue(triggers, snapshot: value)
                }
                if panelIsOpen {
                    trend.append(value)
                    trend.removeAll {
                        value.sampledAt.timeIntervalSince($0.sampledAt) > 60
                    }
                } else {
                    trend.removeAll(keepingCapacity: true)
                }
            }
        }
    }

    func stop() {
        observationTask?.cancel()
        observationTask = nil
        Task { await engine.stop() }
    }

    private var samplingContext: SamplingContext {
        SamplingContext(
            panelIsOpen: panelIsOpen,
            lowBattery: powerState.lowBattery,
            cpuLevel: snapshot.cpuLevel,
            gpuLevel: snapshot.gpuLevel,
            expandedProcess: expandedProcess
        )
    }
}
