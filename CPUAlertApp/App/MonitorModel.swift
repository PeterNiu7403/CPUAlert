import Observation
import SwiftUI

@MainActor
@Observable
final class MonitorModel {
    private(set) var snapshot = MetricsSnapshot.empty
    var selectedResource: ResourceKind = .cpu
    var panelIsOpen = false
    var showTenRows: Bool {
        didSet { settings.showTenRows = showTenRows }
    }
    var expandedProcess: ProcessIdentity?
    var expandedGPUGroupID: UInt64?
    private(set) var trend: [MetricsSnapshot] = []

    @ObservationIgnored private let engine: SamplingEngine
    @ObservationIgnored private let powerState: PowerStateMonitor
    @ObservationIgnored private let notificationService: NotificationService
    @ObservationIgnored private let settings: AppSettings
    @ObservationIgnored private let terminationCoordinator: TerminationCoordinator
    @ObservationIgnored private let fixedSnapshot: MetricsSnapshot?
    @ObservationIgnored private let fixedTrend: [MetricsSnapshot]
    @ObservationIgnored private let clock = ContinuousClock()
    @ObservationIgnored private let startedAt: ContinuousClock.Instant
    @ObservationIgnored private var observationTask: Task<Void, Never>?
    @ObservationIgnored private var alertEngine = AlertEngine()

    init(
        engine: SamplingEngine,
        powerState: PowerStateMonitor,
        notificationService: NotificationService,
        settings: AppSettings,
        terminationCoordinator: TerminationCoordinator,
        fixedSnapshot: MetricsSnapshot? = nil,
        fixedTrend: [MetricsSnapshot] = []
    ) {
        self.engine = engine
        self.powerState = powerState
        self.notificationService = notificationService
        self.settings = settings
        self.terminationCoordinator = terminationCoordinator
        self.fixedSnapshot = fixedSnapshot
        self.fixedTrend = fixedTrend
        showTenRows = settings.showTenRows
        startedAt = clock.now
    }

    func start() {
        if let fixedSnapshot {
            snapshot = fixedSnapshot
            trend = fixedTrend
            return
        }
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
                if settings.notificationsEnabled, !triggers.isEmpty {
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

    func requestNotificationAuthorization() async -> Bool {
        let granted = await notificationService.requestAuthorization()
        settings.notificationsEnabled = granted
        return granted
    }

    func requestGracefulTermination(_ target: ProcessMetric) async -> TerminationResult {
        await terminationCoordinator.requestGraceful(target)
    }

    func requestForceTermination(_ target: ProcessMetric) async -> TerminationResult {
        await terminationCoordinator.requestForce(target)
    }

    func processMetric(for identity: ProcessIdentity) -> ProcessMetric? {
        if let existing = snapshot.processes.first(where: { $0.identity == identity }) {
            return existing
        }
        guard let record = ProcessIdentityReader().currentIdentity(pid: identity.pid),
              record.identity == identity else {
            return nil
        }
        return ProcessMetric(
            identity: identity,
            name: record.name,
            bundleIdentifier: nil,
            ownerUID: record.uid,
            cpuUsage: 0,
            isApplication: false
        )
    }

    var currentCadence: SamplingCadence {
        SamplingPolicy.cadence(for: samplingContext)
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
