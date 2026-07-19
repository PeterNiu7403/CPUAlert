import Foundation
import IOKit.ps
import Observation

private func powerSourceDidChange(_ context: UnsafeMutableRawPointer?) {
    guard let context else { return }
    let address = UInt(bitPattern: context)
    MainActor.assumeIsolated {
        guard let pointer = UnsafeMutableRawPointer(bitPattern: address) else { return }
        let monitor = Unmanaged<PowerStateMonitor>.fromOpaque(pointer).takeUnretainedValue()
        monitor.refresh()
    }
}

@MainActor
@Observable
final class PowerStateMonitor: NSObject {
    private(set) var lowBattery = ProcessInfo.processInfo.isLowPowerModeEnabled

    @ObservationIgnored private var runLoopSource: CFRunLoopSource?

    override init() {
        super.init()
        refresh()

        let context = Unmanaged.passUnretained(self).toOpaque()
        if let source = IOPSNotificationCreateRunLoopSource(powerSourceDidChange, context)?
            .takeRetainedValue() {
            runLoopSource = source
            CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        }
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(powerModeDidChange),
            name: .NSProcessInfoPowerStateDidChange,
            object: nil
        )
    }

    func refresh() {
        lowBattery = ProcessInfo.processInfo.isLowPowerModeEnabled || internalBatteryIsLow()
    }

    func stop() {
        NotificationCenter.default.removeObserver(self)
        if let runLoopSource {
            CFRunLoopRemoveSource(CFRunLoopGetMain(), runLoopSource, .commonModes)
            self.runLoopSource = nil
        }
    }

    @objc
    private func powerModeDidChange() {
        refresh()
    }

    private func internalBatteryIsLow() -> Bool {
        guard let info = IOPSCopyPowerSourcesInfo()?.takeRetainedValue(),
              let sources = IOPSCopyPowerSourcesList(info)?.takeRetainedValue()
                as? [CFTypeRef] else {
            return false
        }

        for source in sources {
            guard let description = IOPSGetPowerSourceDescription(info, source)?
                .takeUnretainedValue() as? [String: Any],
                  description[kIOPSTypeKey] as? String == kIOPSInternalBatteryType,
                  description[kIOPSIsChargingKey] as? Bool == false,
                  let current = description[kIOPSCurrentCapacityKey] as? NSNumber,
                  let maximum = description[kIOPSMaxCapacityKey] as? NSNumber,
                  maximum.doubleValue > 0 else {
                continue
            }
            if current.doubleValue / maximum.doubleValue <= 0.20 {
                return true
            }
        }
        return false
    }
}
