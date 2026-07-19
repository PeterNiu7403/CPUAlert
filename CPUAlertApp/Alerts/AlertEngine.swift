import Foundation

struct AlertTrigger: Equatable, Sendable {
    let resource: ResourceKind
    let level: PressureLevel
}

struct AlertEngine: Sendable {
    private struct State: Sendable {
        var level: PressureLevel = .green
        var enteredAt: Duration = .zero
        var notified = false
        var lastRedNotification: Duration?
    }

    private var states: [ResourceKind: State] = [:]

    mutating func evaluate(
        resource: ResourceKind,
        level: PressureLevel,
        elapsed: Duration
    ) -> [AlertTrigger] {
        var state = states[resource] ?? State()
        guard level != .unavailable, level >= .yellow else {
            states[resource] = State(level: level, enteredAt: elapsed)
            return []
        }
        if state.level != level {
            state = State(level: level, enteredAt: elapsed)
        }

        let sustained = elapsed - state.enteredAt
        let required: Duration = level == .yellow ? .seconds(15)
            : level == .orange ? .seconds(10)
            : .seconds(5)

        guard sustained >= required else {
            states[resource] = state
            return []
        }

        if level == .red {
            if let last = state.lastRedNotification,
               elapsed - last < .seconds(600) {
                states[resource] = state
                return []
            }
            state.lastRedNotification = elapsed
            state.notified = true
            states[resource] = state
            return [AlertTrigger(resource: resource, level: level)]
        }

        guard !state.notified else {
            states[resource] = state
            return []
        }
        state.notified = true
        states[resource] = state
        return [AlertTrigger(resource: resource, level: level)]
    }
}
