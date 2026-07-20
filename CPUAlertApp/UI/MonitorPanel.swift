import AppKit
import SwiftUI

struct MonitorPanel: View {
    @Bindable var model: MonitorModel
    @Bindable var settings: AppSettings
    @Bindable var loginItemService: LoginItemService
    let onOpenSettings: @MainActor () -> Void
    @State private var showMemoryCleanup = false

    var body: some View {
        VStack(spacing: 12) {
            if !settings.hasCompletedFirstRun {
                FirstRunView(
                    settings: settings,
                    loginItemService: loginItemService,
                    model: model
                )
            }
            header
            TrendSparkline(snapshots: model.trend)
                .frame(height: 66)

            Picker("panel.resource", selection: $model.selectedResource) {
                Text("panel.cpu").tag(ResourceKind.cpu)
                Text("panel.gpu").tag(ResourceKind.gpu)
                Text("panel.memory").tag(ResourceKind.memory)
            }
            .pickerStyle(.segmented)
            .labelsHidden()

            HStack {
                Text(rankingTitle)
                    .font(.headline)
                Spacer()
                if model.selectedResource == .memory {
                    Button("action.releaseMemory") {
                        showMemoryCleanup = true
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .accessibilityIdentifier("memory-cleanup-open")
                }
                Picker("panel.rows", selection: $model.showTenRows) {
                    Text("panel.rows5").tag(false)
                    Text("panel.rows10").tag(true)
                }
                .labelsHidden()
                .pickerStyle(.menu)
                .fixedSize()
            }

            ScrollView {
                RankedProcessList(model: model)
                    .padding(.vertical, 2)
            }
            .id(model.selectedResource)
            .frame(minHeight: 150, maxHeight: 260)

            Divider()
            HStack {
                Button(action: onOpenSettings) {
                    Label("action.settings", systemImage: "gearshape")
                }
                .buttonStyle(.plain)
                Spacer()
                Button("action.quit") {
                    NSApplication.shared.terminate(nil)
                }
                .buttonStyle(.plain)
            }
            .font(.caption)
        }
        .padding(14)
        .frame(width: 360)
        .sheet(isPresented: $showMemoryCleanup) {
            MemoryCleanupSheet(model: model)
        }
        .onAppear { model.panelIsOpen = true }
        .onDisappear {
            model.panelIsOpen = false
            model.expandedProcess = nil
            model.expandedGPUGroupID = nil
        }
    }

    private var header: some View {
        HStack(spacing: 10) {
            metricCard(
                title: "panel.cpu",
                usage: model.snapshot.cpuUsage,
                level: model.snapshot.cpuLevel,
                detail: String(localized: model.snapshot.cpuLevel.stringKey)
            )
            metricCard(
                title: "panel.gpu",
                usage: model.snapshot.gpuUsage,
                level: model.snapshot.gpuLevel,
                detail: String(localized: model.snapshot.gpuLevel.stringKey)
            )
            metricCard(
                title: "panel.memory",
                usage: model.snapshot.memoryUsage,
                level: model.snapshot.memoryLevel,
                detail: model.snapshot.memory.map(MemoryFormatting.usedTotal)
                    ?? String(localized: "value.unavailable")
            )
        }
    }

    private func metricCard(
        title: LocalizedStringKey,
        usage: Double?,
        level: PressureLevel,
        detail: String
    ) -> some View {
        let usageText = usage.map {
            $0.formatted(.percent.precision(.fractionLength(0)))
        } ?? "—"
        return VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 4) {
                Text(title)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Spacer(minLength: 2)
                Circle()
                    .fill(level.displayColor)
                    .frame(width: 7, height: 7)
            }
            Text(usageText)
                .font(.title3.bold().monospacedDigit())
            Text(detail)
                .font(.caption2)
                .foregroundStyle(.secondary)
                .lineLimit(1)
                .minimumScaleFactor(0.72)
        }
        .frame(maxWidth: .infinity, minHeight: 57, alignment: .leading)
        .padding(8)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 10))
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            Text(title)
                + Text(", \(usageText), \(detail), ")
                + Text(level.localizedKey)
        )
    }

    private var rankingTitle: LocalizedStringKey {
        switch model.selectedResource {
        case .cpu: "panel.topProcesses"
        case .gpu: "panel.topGroups"
        case .memory: "panel.topMemoryProcesses"
        }
    }
}

private struct MemoryCleanupSheet: View {
    @Bindable var model: MonitorModel
    @Environment(\.dismiss) private var dismiss
    @State private var candidates: [ProcessMetric] = []
    @State private var selected: Set<ProcessIdentity> = []
    @State private var outcomes: [MemoryCleanupOutcome] = []
    @State private var showConfirmation = false
    @State private var isRunning = false

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 4) {
                Text("cleanup.title")
                    .font(.title3.bold())
                Text("cleanup.intro")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            if candidates.isEmpty {
                ContentUnavailableView(
                    "cleanup.empty",
                    systemImage: "checkmark.circle",
                    description: Text("cleanup.empty.detail")
                )
                .frame(maxWidth: .infinity, minHeight: 180)
            } else {
                ScrollView {
                    LazyVStack(spacing: 6) {
                        ForEach(candidates) { process in
                            Toggle(isOn: selectionBinding(for: process.identity)) {
                                HStack(spacing: 9) {
                                    ProcessIcon(identity: process.identity)
                                    VStack(alignment: .leading, spacing: 1) {
                                        Text(process.name).lineLimit(1)
                                        Text(String(
                                            format: String(localized: "panel.process.pid.format"),
                                            locale: .current,
                                            process.identity.pid
                                        ))
                                            .font(.caption2)
                                            .foregroundStyle(.secondary)
                                    }
                                    Spacer(minLength: 8)
                                    Text(MemoryFormatting.bytes(process.physicalFootprintBytes))
                                        .font(.callout.monospacedDigit())
                                }
                            }
                            .toggleStyle(.checkbox)
                            .padding(8)
                            .background(.quaternary, in: RoundedRectangle(cornerRadius: 8))
                            .accessibilityIdentifier(
                                "memory-cleanup-candidate-\(process.identity.pid)"
                            )
                        }
                    }
                    .padding(.vertical, 2)
                }
                .frame(minHeight: 180, maxHeight: 280)
            }

            Text("cleanup.footnote")
                .font(.caption2)
                .foregroundStyle(.secondary)

            if !outcomes.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text("cleanup.results.title").font(.headline)
                    ForEach(outcomes) { outcome in
                        HStack(spacing: 6) {
                            Image(systemName: resultSymbol(outcome.result))
                            Text(outcome.target.name)
                            Spacer()
                            Text(resultText(outcome.result))
                                .foregroundStyle(.secondary)
                        }
                        .font(.caption)
                    }
                }
            }

            Divider()
            HStack {
                Text(selectionSummary)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .monospacedDigit()
                Spacer()
                Button("action.cancel") { dismiss() }
                Button("cleanup.quitSelected") {
                    showConfirmation = true
                }
                .buttonStyle(.borderedProminent)
                .disabled(selected.isEmpty || isRunning)
                .accessibilityIdentifier("memory-cleanup-continue")
            }
        }
        .padding(18)
        .frame(width: 430)
        .task {
            if candidates.isEmpty {
                candidates = model.memoryCleanupCandidates
            }
        }
        .confirmationDialog(
            String(localized: "cleanup.confirm.title"),
            isPresented: $showConfirmation,
            titleVisibility: .visible
        ) {
            Button(confirmButtonTitle, role: .destructive) {
                performCleanup()
            }
            Button("action.cancel", role: .cancel) {}
        } message: {
            Text("cleanup.confirm.message")
        }
    }

    private var selectedProcesses: [ProcessMetric] {
        candidates.filter { selected.contains($0.identity) }
    }

    private var selectionSummary: String {
        String(
            format: String(localized: "cleanup.selection.format"),
            locale: .current,
            selected.count,
            MemoryFormatting.bytes(
                MemoryCleanupPolicy.estimatedFootprint(of: selectedProcesses)
            )
        )
    }

    private var confirmButtonTitle: String {
        String(
            format: String(localized: "cleanup.confirm.action.format"),
            locale: .current,
            selected.count
        )
    }

    private func selectionBinding(for identity: ProcessIdentity) -> Binding<Bool> {
        Binding(
            get: { selected.contains(identity) },
            set: { isSelected in
                if isSelected {
                    selected.insert(identity)
                } else {
                    selected.remove(identity)
                }
            }
        )
    }

    private func performCleanup() {
        let targets = selectedProcesses
        guard !targets.isEmpty else { return }
        isRunning = true
        outcomes = []
        Task {
            outcomes = await model.requestMemoryCleanup(targets)
            selected.removeAll()
            isRunning = false
        }
    }

    private func resultSymbol(_ result: TerminationResult) -> String {
        switch result {
        case .terminated, .notFound: "checkmark.circle.fill"
        case .forceAvailable: "clock.badge.exclamationmark"
        case .identityChanged, .forceNotAvailable, .protectedTarget, .failed:
            "exclamationmark.triangle.fill"
        }
    }

    private func resultText(_ result: TerminationResult) -> String {
        switch result {
        case .terminated: String(localized: "cleanup.result.exited")
        case .forceAvailable: String(localized: "cleanup.result.stillRunning")
        case .identityChanged: String(localized: "termination.result.identityChanged")
        case .forceNotAvailable: String(localized: "termination.result.forceNotAvailable")
        case .protectedTarget: String(localized: "termination.result.protected")
        case .notFound: String(localized: "cleanup.result.alreadyExited")
        case .failed(let errorCode): String(
            format: String(localized: "termination.result.failed.format"),
            locale: .current,
            errorCode
        )
        }
    }
}

private struct TrendSparkline: View {
    let snapshots: [MetricsSnapshot]

    var body: some View {
        VStack(spacing: 5) {
            HStack(spacing: 12) {
                legend("panel.cpu", color: .cyan)
                legend("panel.gpu", color: .purple)
                legend("panel.memory", color: .mint)
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 3)
            Canvas { context, size in
                draw(\.cpuUsage, color: .cyan, in: context, size: size)
                draw(\.gpuUsage, color: .purple, in: context, size: size)
                draw(\.memoryUsage, color: .mint, in: context, size: size)
            }
            .background(.quaternary.opacity(0.55), in: RoundedRectangle(cornerRadius: 8))
        }
        .accessibilityLabel(Text("panel.trend.accessibility"))
    }

    private func legend(_ key: LocalizedStringKey, color: Color) -> some View {
        HStack(spacing: 4) {
            Capsule().fill(color).frame(width: 10, height: 3)
            Text(key).font(.caption2).foregroundStyle(.secondary)
        }
    }

    private func draw(
        _ keyPath: KeyPath<MetricsSnapshot, Double>,
        color: Color,
        in context: GraphicsContext,
        size: CGSize
    ) {
        drawValues(snapshots.map { $0[keyPath: keyPath] }, color: color, in: context, size: size)
    }

    private func draw(
        _ keyPath: KeyPath<MetricsSnapshot, Double?>,
        color: Color,
        in context: GraphicsContext,
        size: CGSize
    ) {
        drawValues(snapshots.map { $0[keyPath: keyPath] }, color: color, in: context, size: size)
    }

    private func drawValues(
        _ values: [Double?],
        color: Color,
        in context: GraphicsContext,
        size: CGSize
    ) {
        guard values.count > 1 else { return }
        var path = Path()
        var hasPoint = false
        for (index, value) in values.enumerated() {
            guard let value else {
                hasPoint = false
                continue
            }
            let x = size.width * CGFloat(index) / CGFloat(values.count - 1)
            let y = size.height * (1 - CGFloat(max(0, min(value, 1))))
            if hasPoint {
                path.addLine(to: CGPoint(x: x, y: y))
            } else {
                path.move(to: CGPoint(x: x, y: y))
                hasPoint = true
            }
        }
        context.stroke(path, with: .color(color), lineWidth: 1.5)
    }
}

extension PressureLevel {
    var localizedKey: LocalizedStringKey {
        switch self {
        case .green: "pressure.normal"
        case .yellow: "pressure.elevated"
        case .orange: "pressure.high"
        case .red: "pressure.critical"
        case .unavailable: "value.unavailable"
        }
    }

    var stringKey: String.LocalizationValue {
        switch self {
        case .green: "pressure.normal"
        case .yellow: "pressure.elevated"
        case .orange: "pressure.high"
        case .red: "pressure.critical"
        case .unavailable: "value.unavailable"
        }
    }
}
