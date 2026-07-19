import Testing
@testable import CPUAlert

struct AlertEngineTests {
    @Test func yellowRequiresFifteenSeconds() {
        var engine = AlertEngine()
        #expect(engine.evaluate(
            resource: .cpu,
            level: .yellow,
            elapsed: .seconds(0)
        ).isEmpty)
        #expect(engine.evaluate(
            resource: .cpu,
            level: .yellow,
            elapsed: .seconds(14)
        ).isEmpty)
        #expect(engine.evaluate(
            resource: .cpu,
            level: .yellow,
            elapsed: .seconds(15)
        ) == [AlertTrigger(resource: .cpu, level: .yellow)])
        #expect(engine.evaluate(
            resource: .cpu,
            level: .yellow,
            elapsed: .seconds(16)
        ).isEmpty)
    }

    @Test func orangeAndRedUseApprovedDurations() {
        var orange = AlertEngine()
        #expect(orange.evaluate(
            resource: .gpu,
            level: .orange,
            elapsed: .seconds(0)
        ).isEmpty)
        #expect(orange.evaluate(
            resource: .gpu,
            level: .orange,
            elapsed: .seconds(9)
        ).isEmpty)
        #expect(orange.evaluate(
            resource: .gpu,
            level: .orange,
            elapsed: .seconds(10)
        ).count == 1)

        var red = AlertEngine()
        #expect(red.evaluate(
            resource: .cpu,
            level: .red,
            elapsed: .seconds(0)
        ).isEmpty)
        #expect(red.evaluate(
            resource: .cpu,
            level: .red,
            elapsed: .seconds(4)
        ).isEmpty)
        #expect(red.evaluate(
            resource: .cpu,
            level: .red,
            elapsed: .seconds(5)
        ).count == 1)
        #expect(red.evaluate(
            resource: .cpu,
            level: .red,
            elapsed: .seconds(604)
        ).isEmpty)
        #expect(red.evaluate(
            resource: .cpu,
            level: .red,
            elapsed: .seconds(605)
        ).count == 1)
    }

    @Test func unavailableResetsPendingAlert() {
        var engine = AlertEngine()
        _ = engine.evaluate(resource: .gpu, level: .yellow, elapsed: .seconds(10))
        #expect(engine.evaluate(
            resource: .gpu,
            level: .unavailable,
            elapsed: .seconds(11)
        ).isEmpty)
        #expect(engine.evaluate(
            resource: .gpu,
            level: .yellow,
            elapsed: .seconds(15)
        ).isEmpty)
    }
}
