enum SamplingPolicy {
    static func cadence(for context: SamplingContext) -> SamplingCadence {
        let elevated = context.cpuLevel >= .yellow
            || context.gpuLevel >= .yellow
            || context.memoryLevel >= .yellow
        if context.panelIsOpen || elevated {
            return SamplingCadence(
                system: .seconds(1),
                ranking: context.expandedProcess == nil ? .seconds(2) : .seconds(1),
                thread: context.expandedProcess == nil ? nil : .seconds(1)
            )
        }
        if context.lowBattery {
            return .lowBattery
        }
        return .background
    }
}
