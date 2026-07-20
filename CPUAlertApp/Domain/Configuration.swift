import Foundation

struct AlertThresholds: Equatable, Sendable {
    static let defaults = AlertThresholds(yellow: 0.70, orange: 0.85, red: 0.95)!

    let yellow: Double
    let orange: Double
    let red: Double
    let hysteresis: Double

    init?(yellow: Double, orange: Double, red: Double, hysteresis: Double = 0.05) {
        guard (0...1).contains(yellow),
              (0...1).contains(orange),
              (0...1).contains(red),
              orange - yellow >= 0.05,
              red - orange >= 0.05,
              (0...0.20).contains(hysteresis) else {
            return nil
        }
        self.yellow = yellow
        self.orange = orange
        self.red = red
        self.hysteresis = hysteresis
    }

    func level(for usage: Double?, previous: PressureLevel) -> PressureLevel {
        guard let usage, usage.isFinite else { return .unavailable }
        let value = max(0, min(usage, 1))
        let raw: PressureLevel = value >= red ? .red
            : value >= orange ? .orange
            : value >= yellow ? .yellow
            : .green

        if previous == .red, raw < .red, value >= red - hysteresis { return .red }
        if previous == .orange, raw < .orange, value >= orange - hysteresis { return .orange }
        if previous == .yellow, raw < .yellow, value >= yellow - hysteresis { return .yellow }
        return raw
    }
}

struct SamplingContext: Equatable, Sendable {
    let panelIsOpen: Bool
    let lowBattery: Bool
    let cpuLevel: PressureLevel
    let gpuLevel: PressureLevel
    let memoryLevel: PressureLevel
    let expandedProcess: ProcessIdentity?

    init(
        panelIsOpen: Bool,
        lowBattery: Bool,
        cpuLevel: PressureLevel,
        gpuLevel: PressureLevel,
        memoryLevel: PressureLevel = .green,
        expandedProcess: ProcessIdentity?
    ) {
        self.panelIsOpen = panelIsOpen
        self.lowBattery = lowBattery
        self.cpuLevel = cpuLevel
        self.gpuLevel = gpuLevel
        self.memoryLevel = memoryLevel
        self.expandedProcess = expandedProcess
    }

    static let closedGreen = SamplingContext(
        panelIsOpen: false, lowBattery: false,
        cpuLevel: .green, gpuLevel: .green, expandedProcess: nil
    )
    static let openGreen = SamplingContext(
        panelIsOpen: true, lowBattery: false,
        cpuLevel: .green, gpuLevel: .green, expandedProcess: nil
    )
    static let closedYellow = SamplingContext(
        panelIsOpen: false, lowBattery: false,
        cpuLevel: .yellow, gpuLevel: .green, expandedProcess: nil
    )
    static let closedGreenLowBattery = SamplingContext(
        panelIsOpen: false, lowBattery: true,
        cpuLevel: .green, gpuLevel: .green, expandedProcess: nil
    )
}

struct SamplingCadence: Equatable, Sendable {
    let system: Duration
    let ranking: Duration
    let thread: Duration?

    static let background = SamplingCadence(
        system: .seconds(2), ranking: .seconds(6), thread: nil
    )
    static let interactive = SamplingCadence(
        system: .seconds(1), ranking: .seconds(2), thread: nil
    )
    static let lowBattery = SamplingCadence(
        system: .seconds(4), ranking: .seconds(12), thread: nil
    )
}
