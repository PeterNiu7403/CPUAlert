import Foundation
import Observation

@MainActor
protocol SettingsStore: AnyObject {
    func storedDouble(forKey key: String) -> Double?
    func storedBool(forKey key: String) -> Bool?
    func set(_ value: Double, forKey key: String)
    func set(_ value: Bool, forKey key: String)
}

extension UserDefaults: SettingsStore {
    func storedDouble(forKey key: String) -> Double? {
        guard object(forKey: key) != nil else { return nil }
        return double(forKey: key)
    }

    func storedBool(forKey key: String) -> Bool? {
        guard object(forKey: key) != nil else { return nil }
        return bool(forKey: key)
    }
}

@MainActor
final class InMemorySettingsStore: SettingsStore {
    private var doubles: [String: Double] = [:]
    private var booleans: [String: Bool] = [:]

    func storedDouble(forKey key: String) -> Double? { doubles[key] }
    func storedBool(forKey key: String) -> Bool? { booleans[key] }
    func set(_ value: Double, forKey key: String) { doubles[key] = value }
    func set(_ value: Bool, forKey key: String) { booleans[key] = value }
}

@MainActor
@Observable
final class AppSettings {
    private enum Key {
        static let yellow = "alerts.yellow"
        static let orange = "alerts.orange"
        static let red = "alerts.red"
        static let notificationsEnabled = "permissions.notificationsEnabled"
        static let launchAtLogin = "permissions.launchAtLogin"
        static let completedFirstRun = "onboarding.completed"
        static let showTenRows = "general.showTenRows"
    }

    private(set) var thresholds: AlertThresholds
    var notificationsEnabled: Bool {
        didSet { store.set(notificationsEnabled, forKey: Key.notificationsEnabled) }
    }
    var launchAtLogin: Bool {
        didSet { store.set(launchAtLogin, forKey: Key.launchAtLogin) }
    }
    var hasCompletedFirstRun: Bool {
        didSet { store.set(hasCompletedFirstRun, forKey: Key.completedFirstRun) }
    }
    var showTenRows: Bool {
        didSet { store.set(showTenRows, forKey: Key.showTenRows) }
    }

    @ObservationIgnored private let store: any SettingsStore
    @ObservationIgnored private let samplingEngine: SamplingEngine?

    init(
        store: any SettingsStore = UserDefaults.standard,
        samplingEngine: SamplingEngine? = nil
    ) {
        self.store = store
        self.samplingEngine = samplingEngine

        let defaults = AlertThresholds.defaults
        thresholds = AlertThresholds(
            yellow: store.storedDouble(forKey: Key.yellow) ?? defaults.yellow,
            orange: store.storedDouble(forKey: Key.orange) ?? defaults.orange,
            red: store.storedDouble(forKey: Key.red) ?? defaults.red
        ) ?? defaults
        notificationsEnabled = store.storedBool(forKey: Key.notificationsEnabled) ?? false
        launchAtLogin = store.storedBool(forKey: Key.launchAtLogin) ?? false
        hasCompletedFirstRun = store.storedBool(forKey: Key.completedFirstRun) ?? false
        showTenRows = store.storedBool(forKey: Key.showTenRows) ?? false
    }

    @discardableResult
    func setThresholds(yellow: Double, orange: Double, red: Double) -> Bool {
        guard let validated = AlertThresholds(
            yellow: yellow,
            orange: orange,
            red: red
        ) else {
            return false
        }

        thresholds = validated
        store.set(validated.yellow, forKey: Key.yellow)
        store.set(validated.orange, forKey: Key.orange)
        store.set(validated.red, forKey: Key.red)
        if let samplingEngine {
            Task { await samplingEngine.updateThresholds(validated) }
        }
        return true
    }
}
