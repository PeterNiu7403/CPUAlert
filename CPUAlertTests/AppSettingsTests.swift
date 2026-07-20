import Testing
@testable import CPUAlert

@MainActor
struct AppSettingsTests {
    @Test func invalidThresholdsDoNotReplaceLastValidValue() {
        let store = InMemorySettingsStore()
        let settings = AppSettings(store: store)
        #expect(settings.thresholds == .defaults)
        #expect(!settings.setThresholds(yellow: 0.80, orange: 0.82, red: 0.95))
        #expect(settings.thresholds == .defaults)
    }

    @Test func validThresholdsPersist() {
        let store = InMemorySettingsStore()
        let settings = AppSettings(store: store)
        #expect(settings.setThresholds(yellow: 0.65, orange: 0.80, red: 0.95))
        #expect(AppSettings(store: store).thresholds == AlertThresholds(
            yellow: 0.65,
            orange: 0.80,
            red: 0.95
        ))
    }
}
