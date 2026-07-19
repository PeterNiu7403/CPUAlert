import AppKit
import SwiftUI

struct MonitorPanel: View {
    @Bindable var model: MonitorModel
    @Bindable var settings: AppSettings
    @Bindable var loginItemService: LoginItemService
    let onOpenSettings: @MainActor () -> Void

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
                .frame(height: 52)

            Picker("panel.resource", selection: $model.selectedResource) {
                Text("panel.cpu").tag(ResourceKind.cpu)
                Text("panel.gpu").tag(ResourceKind.gpu)
            }
            .pickerStyle(.segmented)
            .labelsHidden()

            HStack {
                Text(LocalizedStringKey(
                    model.selectedResource == .cpu ? "panel.topProcesses" : "panel.topGroups"
                ))
                    .font(.headline)
                Spacer()
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
                level: model.snapshot.cpuLevel
            )
            metricCard(
                title: "panel.gpu",
                usage: model.snapshot.gpuUsage,
                level: model.snapshot.gpuLevel
            )
        }
    }

    private func metricCard(
        title: LocalizedStringKey,
        usage: Double?,
        level: PressureLevel
    ) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(usage.map { $0.formatted(.percent.precision(.fractionLength(0))) } ?? "—")
                    .font(.title2.bold().monospacedDigit())
            }
            Spacer()
            Circle()
                .fill(level.displayColor)
                .frame(width: 9, height: 9)
            Text(level.localizedKey)
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
        .padding(10)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 10))
        .accessibilityElement(children: .combine)
    }
}

private struct TrendSparkline: View {
    let snapshots: [MetricsSnapshot]

    var body: some View {
        Canvas { context, size in
            draw(\.cpuUsage, color: .orange, in: context, size: size)
            draw(\.gpuUsage, color: .blue, in: context, size: size)
        }
        .background(.quaternary.opacity(0.55), in: RoundedRectangle(cornerRadius: 8))
        .accessibilityLabel(Text("panel.trend.accessibility"))
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
}
