import SwiftUI

struct MenuBarLabel: View {
    @Bindable var model: MonitorModel
    @State private var lastCPUPercentage: Int?
    @State private var lastGPUPercentage: Int?
    @State private var lastMemoryPercentage: Int?

    var body: some View {
        HStack(spacing: 3) {
            metric(
                symbol: "C",
                percentage: cpuPercentage,
                level: model.snapshot.cpuLevel,
                normalColor: .cyan
            )
            metric(
                symbol: "G",
                percentage: gpuPercentage,
                level: model.snapshot.gpuLevel,
                normalColor: .purple
            )
            metric(
                symbol: "M",
                percentage: memoryPercentage,
                level: model.snapshot.memoryLevel,
                normalColor: .mint
            )
        }
        .frame(width: 78, height: 20)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(accessibilityText)
        .accessibilityIdentifier("menu-bar-triptych")
        .task { model.start() }
        .onAppear { retainValidPercentages() }
        .onChange(of: model.snapshot) { _, _ in retainValidPercentages() }
    }

    private func metric(
        symbol: String,
        percentage: Int?,
        level: PressureLevel,
        normalColor: Color
    ) -> some View {
        VStack(spacing: 2) {
            HStack(alignment: .firstTextBaseline, spacing: 1) {
                Text(symbol)
                    .font(.system(size: 6.5, weight: .bold, design: .rounded))
                    .foregroundStyle(.secondary)
                Spacer(minLength: 0)
                Text(percentage.map(String.init) ?? "—")
                    .font(.system(size: 9, weight: .semibold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(.primary)
            }
            GeometryReader { geometry in
                ZStack(alignment: .leading) {
                    Capsule().fill(indicatorColor(level, normal: normalColor).opacity(0.18))
                    Capsule()
                        .fill(indicatorColor(level, normal: normalColor))
                        .frame(
                            width: geometry.size.width
                                * CGFloat(max(0, min(Double(percentage ?? 0) / 100, 1)))
                        )
                }
            }
            .frame(height: 2.5)
        }
        .frame(width: 24, height: 18)
        .accessibilityHidden(true)
    }

    private var cpuPercentage: Int? {
        model.snapshot.cpuLevel == .unavailable
            ? lastCPUPercentage
            : Int((model.snapshot.cpuUsage * 100).rounded())
    }

    private var gpuPercentage: Int? {
        guard model.snapshot.gpuUsage != nil else { return nil }
        return lastGPUPercentage
    }

    private var memoryPercentage: Int? {
        lastMemoryPercentage
    }

    private var accessibilityText: String {
        let cpu = cpuPercentage.map {
            String(
                format: String(localized: "menu.cpu.accessibility.format"),
                locale: .current,
                $0,
                pressureDescription(model.snapshot.cpuLevel)
            )
        } ?? String(localized: "menu.cpu.accessibility.unavailable")
        let gpu = gpuPercentage.map {
            String(
                format: String(localized: "menu.gpu.accessibility.format"),
                locale: .current,
                $0,
                pressureDescription(model.snapshot.gpuLevel)
            )
        } ?? String(localized: "menu.gpu.accessibility.unavailable")
        let memory = memoryPercentage.map {
            String(
                format: String(localized: "menu.memory.accessibility.format"),
                locale: .current,
                $0,
                pressureDescription(model.snapshot.memoryLevel)
            )
        } ?? String(localized: "menu.memory.accessibility.unavailable")
        return "\(cpu); \(gpu); \(memory)"
    }

    private func retainValidPercentages() {
        if model.snapshot.cpuLevel != .unavailable {
            lastCPUPercentage = Int((model.snapshot.cpuUsage * 100).rounded())
        }
        if let usage = model.snapshot.gpuUsage {
            lastGPUPercentage = Int((usage * 100).rounded())
        }
        if let usage = model.snapshot.memory?.usage {
            lastMemoryPercentage = Int((usage * 100).rounded())
        }
    }

    private func indicatorColor(_ level: PressureLevel, normal: Color) -> Color {
        switch level {
        case .green: normal
        case .yellow, .orange, .red: level.displayColor
        case .unavailable: .secondary
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
