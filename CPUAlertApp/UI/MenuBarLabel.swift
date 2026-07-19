import SwiftUI

struct MenuBarLabel: View {
    @Bindable var model: MonitorModel
    @State private var lastCPUPercentage = 0
    @State private var lastGPUPercentage: Int?

    var body: some View {
        VStack(spacing: 1) {
            row(
                formatKey: "menu.cpu.format",
                percentage: cpuPercentage,
                usage: Double(cpuPercentage) / 100,
                color: model.snapshot.cpuLevel.displayColor
            )
            row(
                formatKey: "menu.gpu.format",
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
        formatKey: String.LocalizationValue,
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
                Text(percentage.map {
                    String(
                        format: String(localized: formatKey),
                        locale: .current,
                        $0
                    )
                } ?? String(localized: "menu.gpu.unavailable"))
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
        let cpu = String(
            format: String(localized: "menu.cpu.accessibility.format"),
            locale: .current,
            cpuPercentage,
            pressureDescription(model.snapshot.cpuLevel)
        )
        let gpu = gpuPercentage.map {
            String(
                format: String(localized: "menu.gpu.accessibility.format"),
                locale: .current,
                $0,
                pressureDescription(model.snapshot.gpuLevel)
            )
        } ?? String(localized: "menu.gpu.accessibility.unavailable")
        return "\(cpu); \(gpu)"
    }

    private func retainValidPercentages() {
        if model.snapshot.cpuLevel != .unavailable {
            lastCPUPercentage = Int((model.snapshot.cpuUsage * 100).rounded())
        }
        if let usage = model.snapshot.gpuUsage {
            lastGPUPercentage = Int((usage * 100).rounded())
        }
    }

    private func pressureDescription(_ level: PressureLevel) -> String {
        switch level {
        case .green: String(localized: "pressure.normal")
        case .yellow: String(localized: "pressure.elevated")
        case .orange: String(localized: "pressure.high")
        case .red: String(localized: "pressure.critical")
        case .unavailable: String(localized: "value.unavailable")
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
