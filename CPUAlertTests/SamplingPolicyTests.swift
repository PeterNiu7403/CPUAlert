import Foundation
import Testing
@testable import CPUAlert

struct SamplingPolicyTests {
    private let thresholds = AlertThresholds.defaults

    @Test func pressureUsesApprovedThresholds() {
        #expect(thresholds.level(for: 0.69, previous: .green) == .green)
        #expect(thresholds.level(for: 0.70, previous: .green) == .yellow)
        #expect(thresholds.level(for: 0.85, previous: .yellow) == .orange)
        #expect(thresholds.level(for: 0.95, previous: .orange) == .red)
        #expect(thresholds.level(for: nil, previous: .red) == .unavailable)
    }

    @Test func pressureUsesFivePointHysteresis() {
        #expect(thresholds.level(for: 0.91, previous: .red) == .red)
        #expect(thresholds.level(for: 0.89, previous: .red) == .orange)
        #expect(thresholds.level(for: 0.81, previous: .orange) == .orange)
        #expect(thresholds.level(for: 0.79, previous: .orange) == .yellow)
        #expect(thresholds.level(for: 0.66, previous: .yellow) == .yellow)
        #expect(thresholds.level(for: 0.64, previous: .yellow) == .green)
    }

    @Test func cadenceAdaptsToVisibilityPressureAndBattery() {
        #expect(SamplingPolicy.cadence(for: .closedGreen) == .background)
        #expect(SamplingPolicy.cadence(for: .openGreen) == .interactive)
        #expect(SamplingPolicy.cadence(for: .closedYellow) == .interactive)
        #expect(SamplingPolicy.cadence(for: .closedGreenLowBattery) == .lowBattery)

        let memoryElevated = SamplingContext(
            panelIsOpen: false,
            lowBattery: false,
            cpuLevel: .green,
            gpuLevel: .green,
            memoryLevel: .yellow,
            expandedProcess: nil
        )
        #expect(SamplingPolicy.cadence(for: memoryElevated) == .interactive)

        let expanded = SamplingContext(
            panelIsOpen: true,
            lowBattery: false,
            cpuLevel: .green,
            gpuLevel: .green,
            expandedProcess: ProcessIdentity(pid: 42, startTimeNanoseconds: 1)
        )
        #expect(SamplingPolicy.cadence(for: expanded) == SamplingCadence(
            system: .seconds(1),
            ranking: .seconds(1),
            thread: .seconds(1)
        ))
    }
}
