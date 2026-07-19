import SwiftUI

struct MenuBarLabel: View {
    @Bindable var model: MonitorModel
    @State private var lastCPUPercentage = 0
    @State private var lastGPUPercentage: Int?

    var body: some View {
        VStack(spacing: 1) {
            row(
                name: "CPU",
                percentage: cpuPercentage,
                usage: Double(cpuPercentage) / 100,
                color: model.snapshot.cpuLevel.displayColor
            )
            row(
                name: "GPU",
                percentage: gpuPercentage,
                usage: model.snapshot.gpuUsage,
                color: model.snapshot.gpuLevel.displayColor
            )
        }
        .frame(width: 52, height: 18)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(accessibilityText)
        .task { model.start() }
        .onAppear { retainValidPercentages() }
        .onChange(of: model.snapshot) { _, _ in retainValidPercentages() }
    }

    private func row(
        name: String,
        percentage: Int?,
        usage: Double?,
        color: Color
    ) -> some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule().fill(color.opacity(0.20))
                Capsule()
                    .fill(color)
                    .frame(width: geometry.size.width * max(0, min(usage ?? 0, 1)))
                Text(percentage.map { "\(name) \($0)%" } ?? "\(name) —")
                    .font(.system(size: 7.5, weight: .semibold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(.primary)
                    .frame(maxWidth: .infinity)
            }
        }
        .frame(height: 8)
    }

    private var cpuPercentage: Int {
        model.snapshot.cpuLevel == .unavailable
            ? lastCPUPercentage
            : Int((model.snapshot.cpuUsage * 100).rounded())
    }

    private var gpuPercentage: Int? {
        guard model.snapshot.gpuUsage != nil else { return nil }
        return lastGPUPercentage
    }

    private var accessibilityText: String {
        let gpu = gpuPercentage.map { "\($0) percent" } ?? "unavailable"
        return "CPU \(cpuPercentage) percent, GPU \(gpu)"
    }

    private func retainValidPercentages() {
        if model.snapshot.cpuLevel != .unavailable {
            lastCPUPercentage = Int((model.snapshot.cpuUsage * 100).rounded())
        }
        if let usage = model.snapshot.gpuUsage {
            lastGPUPercentage = Int((usage * 100).rounded())
        }
    }
}

extension PressureLevel {
    var displayColor: Color {
        switch self {
        case .green: .green
        case .yellow: .yellow
        case .orange: .orange
        case .red: .red
        case .unavailable: .secondary
        }
    }
}
