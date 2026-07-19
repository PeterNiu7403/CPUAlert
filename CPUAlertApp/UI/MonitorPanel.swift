import AppKit
import SwiftUI

struct MonitorPanel: View {
    @Bindable var model: MonitorModel

    var body: some View {
        VStack(spacing: 12) {
            header
            TrendSparkline(snapshots: model.trend)
                .frame(height: 52)

            Picker("Resource", selection: $model.selectedResource) {
                Text("CPU").tag(ResourceKind.cpu)
                Text("GPU").tag(ResourceKind.gpu)
            }
            .pickerStyle(.segmented)
            .labelsHidden()

            HStack {
                Text(model.selectedResource == .cpu ? "Top processes" : "Top process groups")
                    .font(.headline)
                Spacer()
                Picker("Rows", selection: $model.showTenRows) {
                    Text("Top 5").tag(false)
                    Text("Top 10").tag(true)
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
                SettingsLink {
                    Label("Settings", systemImage: "gearshape")
                }
                .buttonStyle(.plain)
                Spacer()
                Button("Quit CPUAlert") {
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
        }
    }

    private var header: some View {
        HStack(spacing: 10) {
            metricCard(
                title: "CPU",
                usage: model.snapshot.cpuUsage,
                level: model.snapshot.cpuLevel
            )
            metricCard(
                title: "GPU",
                usage: model.snapshot.gpuUsage,
                level: model.snapshot.gpuLevel
            )
        }
    }

    private func metricCard(
        title: String,
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
        }
        .padding(10)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 10))
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
        .accessibilityLabel("CPU and GPU activity over the last 60 seconds")
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
