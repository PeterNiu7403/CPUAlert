import SwiftUI

struct MenuBarLabel: View {
    let cpuUsage: Double
    let gpuUsage: Double?
    let cpuColor: Color
    let gpuColor: Color

    var body: some View {
        VStack(spacing: 1) {
            row(name: "CPU", usage: cpuUsage, color: cpuColor)
            row(name: "GPU", usage: gpuUsage, color: gpuColor)
        }
        .frame(width: 52, height: 18)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(accessibilityText)
    }

    private func row(name: String, usage: Double?, color: Color) -> some View {
        GeometryReader { geometry in
            ZStack(alignment: .leading) {
                Capsule().fill(color.opacity(0.20))
                Capsule()
                    .fill(color)
                    .frame(width: geometry.size.width * max(0, min(usage ?? 0, 1)))
                Text(usage.map { "\(name) \(Int(($0 * 100).rounded()))%" } ?? "\(name) —")
                    .font(.system(size: 7.5, weight: .semibold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(.primary)
                    .frame(maxWidth: .infinity)
            }
        }
        .frame(height: 8)
    }

    private var accessibilityText: String {
        let cpu = Int((cpuUsage * 100).rounded())
        let gpu = gpuUsage.map { "\(Int(($0 * 100).rounded())) percent" } ?? "unavailable"
        return "CPU \(cpu) percent, GPU \(gpu)"
    }
}

#Preview {
    MenuBarLabel(
        cpuUsage: 0.42,
        gpuUsage: 0.18,
        cpuColor: .green,
        gpuColor: .green
    )
}
