enum SamplingPolicy {
    static func cadence(for context: SamplingContext) -> SamplingCadence {
        let elevated = context.cpuLevel >= .yellow || context.gpuLevel >= .yellow
        if context.panelIsOpen || elevated {
            return SamplingCadence(
                system: .seconds(1),
                ranking: .seconds(1),
                thread: context.expandedProcess == nil ? nil : .seconds(1)
            )
        }
        if context.lowBattery {
            return .lowBattery
        }
        return .background
    }
}
