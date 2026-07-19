# CPUAlert Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a lightweight native macOS menu bar application that continuously reports normalized CPU and GPU utilization, identifies likely high-usage processes or application groups, raises sustained-load alerts, and safely terminates selected processes.

**Architecture:** A SwiftUI-first menu bar application owns an observable presentation model fed by a single actor-based sampling engine. Public Mach and libproc collectors provide CPU data; a fail-closed Darwin adapter dynamically loads IOReport and coalition functions for GPU data. Same-user termination stays in the app, while Root termination crosses a narrow, signed XPC boundary to an on-demand legacy `SMJobBless` helper.

**Tech Stack:** Swift 6, SwiftUI, Observation, Swift Concurrency, AppKit, Mach, libproc, IOKit/IOReport, UserNotifications, LocalAuthentication, ServiceManagement, Security, XPC, Swift Testing, XCTest, Metal.

## Global Constraints

- Support Apple Silicon only and set `MACOSX_DEPLOYMENT_TARGET = 15.0`.
- Build with Swift 6 strict concurrency checking enabled.
- Use `MenuBarExtra(.window)` first; fall back to `NSStatusItem` only if the Task 1 visual spike proves that SwiftUI cannot render the required colored label correctly.
- Keep the menu bar label approximately 52 points wide with two vertically stacked colored rows: `CPU 42%` and `GPU 18%`.
- Normalize system and per-process CPU readings to a whole-machine `0...100%` scale.
- Rank CPU by process; collect and display thread data only while a process row is expanded.
- Report whole-machine GPU utilization when available. Rank GPU culprits by resource-coalition activity share and label that value as an attribution estimate, not per-process GPU utilization.
- Treat IOReport and coalition access as optional. Display `GPU —`, stop GPU alerts, and continue CPU monitoring if both GPU sources fail.
- Use green below 70%, yellow at 70-84%, orange at 85-94%, and red at 95% or above. Apply thresholds independently to CPU and GPU.
- Require sustained load before notification: yellow 15 seconds, orange 10 seconds, red 5 seconds. Require usage to fall 5 percentage points below a threshold before leaving that level.
- Send yellow and orange once per event. Repeat sustained red no more than once every 10 minutes.
- Sample system metrics every 2 seconds and rankings every 6 seconds while the panel is closed and both resources are green. Use 1-second sampling while the panel is open or either resource is elevated. Use 4-second system and 12-second ranking sampling on low battery while closed and green.
- Keep closed-panel green-state averages at or below 0.3% CPU, 40 MB resident memory, and 1 wakeup per second over a 5-minute Release-build measurement.
- Keep the helper on demand with no idle resident process.
- Do not launch `ps`, `top`, or a persistent `powermetrics` process.
- Do not add third-party runtime dependencies, telemetry, networking, process-history persistence, temperature, memory, fan, disk, or network monitoring.
- Localize visible copy in English and Simplified Chinese with a String Catalog.
- Ask for notification permission and login-at-launch preference from the panel on first use; do not show an automatic standalone window.
- Never expose arbitrary command execution through the privileged helper.
- Protect PID 0, PID 1, `kernel_task`, `launchd`, `loginwindow`, `WindowServer`, CPUAlert, and the CPUAlert helper from termination.
- Use `SIGTERM` first. Offer `SIGKILL` only after the exact process identity remains alive for 3 seconds and the user confirms again.
- Require Touch ID or the device passcode for every Root termination request.
- This is a local development build. Use the deliberately selected legacy `SMJobBless` path, but isolate the deprecated API and document that it may disappear in a future macOS release.

## Prerequisite

The current machine has Swift 6.3.3 command-line tools but not an active full Xcode installation. Do not begin Task 1 until these commands succeed:

```bash
sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer
xcodebuild -version
xcrun --sdk macosx --show-sdk-path
```

Expected: a full Xcode version, build version, and an SDK path under `/Applications/Xcode.app`.

## File Map

| Path | Responsibility |
|---|---|
| `.gitignore` | Exclude Xcode user state, DerivedData, traces, and local build artifacts. |
| `Config/Base.xcconfig` | Shared deployment target, architecture, bundle prefix, and strict-concurrency settings. |
| `CPUAlert.xcodeproj/` | Native Xcode project and shared schemes. |
| `CPUAlertApp/App/CPUAlertApp.swift` | App entry point, `MenuBarExtra`, Settings scene, and dependency composition. |
| `CPUAlertApp/App/MonitorModel.swift` | Main-actor observable state consumed by SwiftUI. |
| `CPUAlertApp/Domain/Metrics.swift` | Immutable metric, process, thread, and GPU-group value types. |
| `CPUAlertApp/Domain/Configuration.swift` | Alert thresholds, sampling context, cadence, and validation. |
| `CPUAlertApp/Monitoring/CollectorProtocols.swift` | Narrow collector interfaces and explicit GPU availability model. |
| `CPUAlertApp/Monitoring/SamplingPolicy.swift` | Pure adaptive-cadence and pressure-level functions. |
| `CPUAlertApp/Monitoring/DarwinBridge.h` | Stable C boundary for Mach, libproc, IOReport, and coalition calls. |
| `CPUAlertApp/Monitoring/DarwinBridge.c` | Darwin system-call implementation and dynamic private-symbol loading. |
| `CPUAlertApp/Monitoring/CPUCollectors.swift` | Stateful system, process, and thread CPU delta collectors. |
| `CPUAlertApp/Monitoring/GPUCollectors.swift` | GPU utilization, coalition-delta attribution, and fallback handling. |
| `CPUAlertApp/Monitoring/PowerStateMonitor.swift` | Public IOPowerSources-backed low-battery and Low Power Mode state. |
| `CPUAlertApp/Monitoring/SamplingEngine.swift` | Single actor loop that schedules collectors and publishes snapshots. |
| `CPUAlertApp/Alerts/AlertEngine.swift` | Sustained-level, hysteresis, and red-cooldown state machine. |
| `CPUAlertApp/Alerts/NotificationService.swift` | Notification authorization, short merge window, and delivery. |
| `CPUAlertApp/Termination/ProtectedProcessPolicy.swift` | Central process-deny policy shared by UI and helper. |
| `CPUAlertApp/Termination/TerminationCoordinator.swift` | GUI, same-user signal, Root XPC, and force-confirmation orchestration. |
| `CPUAlertApp/Termination/HelperClient.swift` | Typed XPC client and helper lifecycle status. |
| `CPUAlertApp/Termination/LegacyBlessingInstaller.h` | Objective-C interface hiding deprecated ServiceManagement declarations. |
| `CPUAlertApp/Termination/LegacyBlessingInstaller.m` | `SMJobBless` and `SMJobRemove` calls only. |
| `CPUAlertApp/Settings/AppSettings.swift` | Validated local preferences and first-run state. |
| `CPUAlertApp/Settings/LoginItemService.swift` | `SMAppService.mainApp` registration. |
| `CPUAlertApp/UI/MenuBarLabel.swift` | Fixed-size two-row menu bar rendering. |
| `CPUAlertApp/UI/MonitorPanel.swift` | Header, resource switcher, ranking list, trend, and actions. |
| `CPUAlertApp/UI/RankedProcessList.swift` | CPU process/thread and GPU application-group rows. |
| `CPUAlertApp/UI/FirstRunView.swift` | In-panel notification and login-item onboarding. |
| `CPUAlertApp/UI/SettingsView.swift` | General, alerts, privilege, and diagnostics settings. |
| `CPUAlertApp/Resources/Localizable.xcstrings` | English and Simplified Chinese visible copy. |
| `CPUAlertShared/HelperProtocol.swift` | Secure-coding XPC operation, request, response, and service protocol. |
| `CPUAlertShared/ProcessIdentityReader.swift` | PID start-time and executable identity checks compiled into both targets. |
| `CPUAlertHelper/main.swift` | Privileged listener process entry point. |
| `CPUAlertHelper/HelperService.swift` | Caller validation, protected-process checks, fixed operations, and idle exit. |
| `CPUAlertHelper/Helper-Info.plist` | Embedded helper metadata and `SMAuthorizedClients`. |
| `CPUAlertHelper/Launchd.plist` | Embedded job label and Mach service. |
| `CPUAlertTests/` | Swift Testing suites for policy, collectors, alerts, and termination. |
| `CPUAlertUITests/` | XCTest flows using deterministic injected snapshots. |
| `TestFixtures/CPUStress/main.swift` | Disposable controlled CPU load. |
| `TestFixtures/GPUStress/main.swift` | Disposable Metal compute load. |
| `TestFixtures/GPUStress/Stress.metal` | GPU fixture kernel. |
| `Scripts/benchmark.sh` | Repeatable Release-build Time Profiler measurement. |
| `README.md` | Build, privacy, metric semantics, privilege, uninstall, and limitation notes. |

---

### Task 1: Native Project and Menu Bar Rendering Gate

**Files:**
- Create: `.gitignore`
- Create: `Config/Base.xcconfig`
- Create: `CPUAlert.xcodeproj/project.pbxproj`
- Create: `CPUAlert.xcodeproj/xcshareddata/xcschemes/CPUAlert.xcscheme`
- Create: `CPUAlertApp/Info.plist`
- Create: `CPUAlertApp/App/CPUAlertApp.swift`
- Create: `CPUAlertApp/UI/MenuBarLabel.swift`
- Create: `CPUAlertApp/Resources/Assets.xcassets/Contents.json`
- Create: `CPUAlertApp/Resources/Assets.xcassets/AppIcon.appiconset/Contents.json`

**Interfaces:**
- Consumes: Full Xcode selected by the prerequisite commands.
- Produces: An arm64-only `CPUAlert` app target, `CPUAlertHelper`, `CPUAlertTests`, `CPUAlertUITests`, `CPUStress`, and `GPUStress` targets; a validated menu-bar rendering choice used by all later tasks.

- [x] **Step 1: Initialize source control and ignore generated files**

Create `.gitignore` with:

```gitignore
.DS_Store
DerivedData/
build/
*.xcuserstate
xcuserdata/
*.trace
*.xcresult
```

Run:

```bash
git init
git status --short
```

Expected: an initialized repository and only authored project files reported as untracked.

- [x] **Step 2: Create the Xcode targets and shared configuration**

Create a native macOS App project in the repository root. Add the six targets listed in the Interfaces block, make the `CPUAlert` scheme shared, and assign `Config/Base.xcconfig` to Debug and Release configurations.

Use this complete shared configuration:

```xcconfig
MACOSX_DEPLOYMENT_TARGET = 15.0
ARCHS = arm64
ONLY_ACTIVE_ARCH[config=Debug] = YES
SWIFT_VERSION = 6.0
SWIFT_STRICT_CONCURRENCY = complete
CLANG_ENABLE_MODULES = YES
CPU_ALERT_BUNDLE_PREFIX = com.cpualert
CODE_SIGN_STYLE = Automatic
DEVELOPMENT_TEAM = $(CPU_ALERT_DEVELOPMENT_TEAM)
ENABLE_HARDENED_RUNTIME = YES
```

Define `CPU_ALERT_DEVELOPMENT_TEAM` in Xcode's user-defined build settings or an untracked local xcconfig. Do not commit a personal Team ID. Set the app target's product bundle identifier to `$(CPU_ALERT_BUNDLE_PREFIX).app`, the helper target to `$(CPU_ALERT_BUNDLE_PREFIX).helper`, and the XPC Mach service name to `$(CPU_ALERT_BUNDLE_PREFIX).helper.xpc`.

- [x] **Step 3: Configure the app as an agent application**

Set `CPUAlertApp/Info.plist` to include:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>CPUAlert</string>
    <key>LSMinimumSystemVersion</key>
    <string>15.0</string>
    <key>LSUIElement</key>
    <true/>
</dict>
</plist>
```

- [x] **Step 4: Write the static menu bar visual spike**

Create `MenuBarLabel.swift` with a reusable row and deterministic preview values:

```swift
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
```

- [x] **Step 5: Mount the spike in `MenuBarExtra`**

Create the app entry point:

```swift
import SwiftUI

@main
struct CPUAlertApp: App {
    var body: some Scene {
        MenuBarExtra {
            Text("CPUAlert rendering spike")
                .padding()
        } label: {
            MenuBarLabel(
                cpuUsage: 0.42,
                gpuUsage: 0.18,
                cpuColor: .green,
                gpuColor: .green
            )
        }
        .menuBarExtraStyle(.window)

        Settings {
            Text("CPUAlert Settings")
                .frame(width: 420, height: 260)
        }
    }
}
```

- [x] **Step 6: Build and perform the rendering gate**

Run:

```bash
xcodebuild build \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64'
open "$HOME/Library/Developer/Xcode/DerivedData"/*/Build/Products/Debug/CPUAlert.app
```

Expected: build succeeds; the menu bar shows two colored, uncropped rows; `100%` does not resize the item; native hover/highlight remains legible in light and dark appearances.

If colors are stripped or the 18-point label is clipped, record the observed failure in this plan's Task 1 notes and replace only the status-label host with `NSStatusItem` plus `NSHostingView`. Keep the panel SwiftUI-based and keep `MenuBarLabel` unchanged.

**Task 1 rendering note (2026-07-19):** The signed `MenuBarExtra` spike rendered only the `CPU 42%` row in the real menu bar; the GPU row was clipped. The status-label host was therefore replaced with `NSStatusItem` and a pass-through `NSHostingView`, while `MenuBarLabel`, the SwiftUI popover content, and the Settings scene remain SwiftUI-based.

- [x] **Step 7: Commit the rendering decision**

```bash
git add .gitignore Config CPUAlert.xcodeproj CPUAlertApp
git commit -m "feat: scaffold CPUAlert menu bar app"
```

---

### Task 2: Metric Domain and Adaptive Sampling Policy

**Files:**
- Create: `CPUAlertApp/Domain/Metrics.swift`
- Create: `CPUAlertApp/Domain/Configuration.swift`
- Create: `CPUAlertApp/Monitoring/CollectorProtocols.swift`
- Create: `CPUAlertApp/Monitoring/SamplingPolicy.swift`
- Create: `CPUAlertTests/SamplingPolicyTests.swift`

**Interfaces:**
- Consumes: No application runtime state.
- Produces: `ResourceKind`, `PressureLevel`, `ProcessIdentity`, `ProcessMetric`, `ThreadMetric`, `GPUGroupMetric`, `MetricsSnapshot`, `AlertThresholds`, `SamplingContext`, `SamplingCadence`, `SamplingPolicy`, `SystemCPUCollecting`, `ProcessCPUCollecting`, and `GPUCollecting`.

- [x] **Step 1: Define the failing threshold and cadence tests**

Create `CPUAlertTests/SamplingPolicyTests.swift`:

```swift
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
    }
}
```

- [x] **Step 2: Run the focused test and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/SamplingPolicyTests
```

Expected: compilation fails because the domain and policy types do not exist.

- [x] **Step 3: Add the immutable metric types**

Create `Metrics.swift` with these exact public shapes:

```swift
import Foundation

enum ResourceKind: String, CaseIterable, Codable, Sendable {
    case cpu
    case gpu
}

enum PressureLevel: Int, Codable, Comparable, Sendable {
    case unavailable = -1
    case green = 0
    case yellow = 1
    case orange = 2
    case red = 3

    static func < (lhs: Self, rhs: Self) -> Bool {
        lhs.rawValue < rhs.rawValue
    }
}

struct ProcessIdentity: Hashable, Codable, Sendable {
    let pid: Int32
    let startTimeNanoseconds: UInt64
}

struct ProcessMetric: Identifiable, Equatable, Sendable {
    var id: ProcessIdentity { identity }
    let identity: ProcessIdentity
    let name: String
    let bundleIdentifier: String?
    let ownerUID: UInt32
    let cpuUsage: Double
    let isApplication: Bool
}

struct ThreadMetric: Identifiable, Equatable, Sendable {
    let id: UInt64
    let process: ProcessIdentity
    let name: String?
    let cpuUsage: Double
}

struct GPUGroupMetric: Identifiable, Equatable, Sendable {
    let id: UInt64
    let name: String
    let leader: ProcessIdentity?
    let members: [ProcessIdentity]
    let activityShare: Double
}

enum GPUSource: String, Equatable, Sendable {
    case ioReport
    case ioAccelerator
    case unavailable
}

struct MetricsSnapshot: Equatable, Sendable {
    let cpuUsage: Double
    let gpuUsage: Double?
    let processes: [ProcessMetric]
    let gpuGroups: [GPUGroupMetric]
    let expandedThreads: [ThreadMetric]
    let cpuLevel: PressureLevel
    let gpuLevel: PressureLevel
    let gpuSource: GPUSource
    let sampledAt: Date

    static let empty = MetricsSnapshot(
        cpuUsage: 0,
        gpuUsage: nil,
        processes: [],
        gpuGroups: [],
        expandedThreads: [],
        cpuLevel: .green,
        gpuLevel: .unavailable,
        gpuSource: .unavailable,
        sampledAt: .distantPast
    )
}
```

- [x] **Step 4: Add validated thresholds and sampling context**

Create `Configuration.swift`:

```swift
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
    let expandedProcess: ProcessIdentity?

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
        system: .seconds(1), ranking: .seconds(1), thread: nil
    )
    static let lowBattery = SamplingCadence(
        system: .seconds(4), ranking: .seconds(12), thread: nil
    )
}
```

- [x] **Step 5: Add collector protocols and the pure cadence policy**

Create `CollectorProtocols.swift`:

```swift
protocol SystemCPUCollecting: Sendable {
    func sampleSystemCPU() async throws -> Double?
}

protocol ProcessCPUCollecting: Sendable {
    func sampleProcesses() async throws -> [ProcessMetric]
    func sampleThreads(for process: ProcessIdentity) async throws -> [ThreadMetric]
}

protocol GPUCollecting: Sendable {
    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource)
    func sampleGroups() async throws -> [GPUGroupMetric]
}
```

Create `SamplingPolicy.swift`:

```swift
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
```

- [x] **Step 6: Run the policy tests**

Run the Step 2 command again.

Expected: all three tests pass.

- [x] **Step 7: Commit the domain boundary**

```bash
git add CPUAlertApp/Domain CPUAlertApp/Monitoring/CollectorProtocols.swift CPUAlertApp/Monitoring/SamplingPolicy.swift CPUAlertTests/SamplingPolicyTests.swift
git commit -m "feat: define metrics and adaptive sampling policy"
```

---

### Task 3: Whole-Machine, Process, and Thread CPU Collectors

**Files:**
- Create: `CPUAlertApp/Monitoring/DarwinBridge.h`
- Create: `CPUAlertApp/Monitoring/DarwinBridge.c`
- Create: `CPUAlertApp/Monitoring/CPUCollectors.swift`
- Create: `CPUAlertTests/CPUCollectorTests.swift`
- Modify: `CPUAlert.xcodeproj/project.pbxproj`

**Interfaces:**
- Consumes: `SystemCPUCollecting`, `ProcessCPUCollecting`, `ProcessIdentity`, `ProcessMetric`, and `ThreadMetric` from Task 2.
- Produces: `SystemCPUCollector` and `ProcessCPUCollector`, with all public utilization values normalized to `0...1` for the whole machine.

- [x] **Step 1: Write failing delta-calculation tests**

Create `CPUCollectorTests.swift`:

```swift
import Testing
@testable import CPUAlert

struct CPUCollectorTests {
    @Test func systemTicksProduceWholeMachineUsage() {
        let previous = SystemCPUTicks(user: 100, system: 50, idle: 850, nice: 0)
        let current = SystemCPUTicks(user: 130, system: 70, idle: 900, nice: 0)
        #expect(SystemCPUCollector.usage(previous: previous, current: current) == 0.5)
    }

    @Test func processTimeIsNormalizedAcrossLogicalCPUs() {
        let usage = ProcessCPUCollector.normalizedUsage(
            previousNanoseconds: 1_000_000_000,
            currentNanoseconds: 3_000_000_000,
            elapsedNanoseconds: 1_000_000_000,
            logicalCPUCount: 10
        )
        #expect(usage == 0.2)
    }

    @Test func counterRegressionDropsTheSample() {
        #expect(ProcessCPUCollector.normalizedUsage(
            previousNanoseconds: 3,
            currentNanoseconds: 2,
            elapsedNanoseconds: 1,
            logicalCPUCount: 10
        ) == nil)
    }
}
```

- [x] **Step 2: Run the collector tests and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/CPUCollectorTests
```

Expected: compilation fails because the CPU collectors and tick type do not exist.

- [x] **Step 3: Define the stable C bridge**

Create `DarwinBridge.h` with no Objective-C or Swift types:

```c
#ifndef CPUALERT_DARWIN_BRIDGE_H
#define CPUALERT_DARWIN_BRIDGE_H

#include <stdbool.h>
#include <stdint.h>
#include <sys/types.h>

typedef struct {
    uint64_t user;
    uint64_t system;
    uint64_t idle;
    uint64_t nice;
} CPUASystemTicks;

typedef struct {
    pid_t pid;
    uint64_t start_time_ns;
    uint64_t cpu_time_ns;
    uint32_t uid;
    char name[256];
} CPUAProcessCounter;

typedef struct {
    uint64_t thread_id;
    uint64_t cpu_time_ns;
    char name[64];
} CPUAThreadCounter;

bool CPUACopySystemTicks(CPUASystemTicks *output);
int CPUACopyAllPIDs(pid_t *buffer, int buffer_bytes);
bool CPUACopyProcessCounter(pid_t pid, CPUAProcessCounter *output);
int CPUACopyThreadIDs(pid_t pid, uint64_t *buffer, int buffer_bytes);
bool CPUACopyThreadCounter(pid_t pid, uint64_t thread_id, CPUAThreadCounter *output);

#endif
```

- [x] **Step 4: Implement public Mach and libproc primitives**

In `DarwinBridge.c`, implement the bridge using these exact sources:

```c
#include "DarwinBridge.h"

#include <libproc.h>
#include <mach/mach.h>
#include <mach/mach_host.h>
#include <string.h>
#include <sys/proc_info.h>
#include <sys/resource.h>

bool CPUACopySystemTicks(CPUASystemTicks *output) {
    if (output == NULL) return false;
    host_cpu_load_info_data_t info = {0};
    mach_msg_type_number_t count = HOST_CPU_LOAD_INFO_COUNT;
    kern_return_t result = host_statistics(
        mach_host_self(), HOST_CPU_LOAD_INFO,
        (host_info_t)&info, &count
    );
    if (result != KERN_SUCCESS) return false;
    output->user = info.cpu_ticks[CPU_STATE_USER];
    output->system = info.cpu_ticks[CPU_STATE_SYSTEM];
    output->idle = info.cpu_ticks[CPU_STATE_IDLE];
    output->nice = info.cpu_ticks[CPU_STATE_NICE];
    return true;
}

int CPUACopyAllPIDs(pid_t *buffer, int buffer_bytes) {
    return proc_listpids(PROC_ALL_PIDS, 0, buffer, buffer_bytes);
}

bool CPUACopyProcessCounter(pid_t pid, CPUAProcessCounter *output) {
    if (output == NULL || pid <= 0) return false;
    struct proc_bsdinfo bsd = {0};
    if (proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, &bsd, sizeof(bsd)) != sizeof(bsd)) {
        return false;
    }
    rusage_info_current usage = {0};
    if (proc_pid_rusage(pid, RUSAGE_INFO_CURRENT, (rusage_info_t *)&usage) != 0) {
        return false;
    }
    memset(output, 0, sizeof(*output));
    output->pid = pid;
    output->start_time_ns = (uint64_t)bsd.pbi_start_tvsec * 1000000000ULL
        + (uint64_t)bsd.pbi_start_tvusec * 1000ULL;
    output->cpu_time_ns = usage.ri_user_time + usage.ri_system_time;
    output->uid = bsd.pbi_uid;
    strlcpy(output->name, bsd.pbi_name, sizeof(output->name));
    return true;
}

int CPUACopyThreadIDs(pid_t pid, uint64_t *buffer, int buffer_bytes) {
    return proc_pidinfo(pid, PROC_PIDLISTTHREADS, 0, buffer, buffer_bytes);
}

bool CPUACopyThreadCounter(pid_t pid, uint64_t thread_id, CPUAThreadCounter *output) {
    if (output == NULL || pid <= 0 || thread_id == 0) return false;
    struct proc_threadinfo info = {0};
    int size = proc_pidinfo(pid, PROC_PIDTHREADINFO, thread_id, &info, sizeof(info));
    if (size != sizeof(info)) return false;
    memset(output, 0, sizeof(*output));
    output->thread_id = thread_id;
    output->cpu_time_ns = info.pth_user_time + info.pth_system_time;
    strlcpy(output->name, info.pth_name, sizeof(output->name));
    return true;
}
```

Add the C file to the app target and expose the header through the app target's bridging header.

- [x] **Step 5: Implement the pure CPU formulas first**

Create the initial portion of `CPUCollectors.swift`:

```swift
import AppKit
import Foundation

struct SystemCPUTicks: Equatable, Sendable {
    let user: UInt64
    let system: UInt64
    let idle: UInt64
    let nice: UInt64
}

actor SystemCPUCollector: SystemCPUCollecting {
    private var previous: SystemCPUTicks?

    static func usage(previous: SystemCPUTicks, current: SystemCPUTicks) -> Double? {
        guard current.user >= previous.user,
              current.system >= previous.system,
              current.idle >= previous.idle,
              current.nice >= previous.nice else { return nil }
        let busy = current.user - previous.user
            + current.system - previous.system
            + current.nice - previous.nice
        let total = busy + current.idle - previous.idle
        guard total > 0 else { return nil }
        return max(0, min(Double(busy) / Double(total), 1))
    }

    func sampleSystemCPU() async throws -> Double? {
        var raw = CPUASystemTicks()
        guard CPUACopySystemTicks(&raw) else { return nil }
        let current = SystemCPUTicks(
            user: raw.user, system: raw.system,
            idle: raw.idle, nice: raw.nice
        )
        defer { previous = current }
        guard let previous else { return nil }
        return Self.usage(previous: previous, current: current)
    }
}

actor ProcessCPUCollector: ProcessCPUCollecting {
    private struct Baseline: Sendable {
        let cpuNanoseconds: UInt64
        let observedNanoseconds: UInt64
    }

    private var processBaselines: [ProcessIdentity: Baseline] = [:]
    private var threadBaselines: [ProcessIdentity: [UInt64: Baseline]] = [:]
    private let logicalCPUCount = max(ProcessInfo.processInfo.activeProcessorCount, 1)

    static func normalizedUsage(
        previousNanoseconds: UInt64,
        currentNanoseconds: UInt64,
        elapsedNanoseconds: UInt64,
        logicalCPUCount: Int
    ) -> Double? {
        guard currentNanoseconds >= previousNanoseconds,
              elapsedNanoseconds > 0,
              logicalCPUCount > 0 else { return nil }
        let value = Double(currentNanoseconds - previousNanoseconds)
            / Double(elapsedNanoseconds)
            / Double(logicalCPUCount)
        return value.isFinite ? max(0, min(value, 1)) : nil
    }
}
```

- [x] **Step 6: Complete process and on-demand thread sampling**

Implement `sampleProcesses()` by first asking `CPUACopyAllPIDs(nil, 0)` for required bytes, allocating a PID array, and retrying once if the second call reports a larger byte count. For each PID, call `CPUACopyProcessCounter`, create `ProcessIdentity(pid:startTimeNanoseconds:)`, and calculate deltas against the matching identity only.

Use `DispatchTime.now().uptimeNanoseconds` as the observation clock. Drop an entry when elapsed time is zero, exceeds 30 seconds, or either counter regresses. Resolve `NSRunningApplication(processIdentifier:)` on the main actor after collecting numeric rows; use it only for bundle identifier and application status. Sort descending, retain the first 20 rows internally, and publish the first 10 requested by the UI.

Implement `sampleThreads(for:)` with the same two-pass buffer pattern around `CPUACopyThreadIDs`. Before returning, call `CPUACopyProcessCounter` and reject the entire result unless its start time still equals `process.startTimeNanoseconds`. Keep thread baselines only for the currently expanded process, normalize with the same whole-machine denominator, and return the top 10 threads.

- [x] **Step 7: Run unit tests and a fixture smoke check**

Run:

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/CPUCollectorTests
```

Expected: all three unit tests pass. A temporary diagnostic invocation of both collectors must return either an initial nil baseline or finite values in `0...1`; no PID reuse produces a delta.

- [x] **Step 8: Commit CPU collection**

```bash
git add CPUAlert.xcodeproj CPUAlertApp/Monitoring/DarwinBridge.h CPUAlertApp/Monitoring/DarwinBridge.c CPUAlertApp/Monitoring/CPUCollectors.swift CPUAlertTests/CPUCollectorTests.swift
git commit -m "feat: collect normalized CPU metrics"
```

---

### Task 4: Fail-Closed GPU Utilization and Coalition Attribution

**Files:**
- Modify: `CPUAlertApp/Monitoring/DarwinBridge.h`
- Modify: `CPUAlertApp/Monitoring/DarwinBridge.c`
- Create: `CPUAlertApp/Monitoring/GPUCollectors.swift`
- Create: `CPUAlertTests/GPUCollectorTests.swift`
- Create: `CPUAlertTests/Fixtures/io-report-single-die.plist`
- Create: `CPUAlertTests/Fixtures/io-report-multi-die.plist`

**Interfaces:**
- Consumes: `GPUCollecting`, `GPUSource`, `GPUGroupMetric`, `ProcessIdentity`, and process-counter primitives from the Darwin bridge.
- Produces: `SystemGPUCollector` and `CoalitionGPUCollector`; both return explicit unavailable results rather than throwing through the monitoring loop.

- [x] **Step 1: Write failing pure aggregation tests**

Create `GPUCollectorTests.swift`:

```swift
import Testing
@testable import CPUAlert

struct GPUCollectorTests {
    @Test func activeResidencyIsWeightedAcrossChannels() {
        let channels = [
            GPUResidency(active: 40, total: 100),
            GPUResidency(active: 30, total: 100)
        ]
        #expect(SystemGPUCollector.aggregate(channels) == 0.35)
    }

    @Test func emptyResidencyIsUnavailable() {
        #expect(SystemGPUCollector.aggregate([]) == nil)
        #expect(SystemGPUCollector.aggregate([.init(active: 0, total: 0)]) == nil)
    }

    @Test func coalitionDeltasBecomeShares() {
        let shares = CoalitionGPUCollector.shares(
            previous: [10: 100, 20: 200],
            current: [10: 130, 20: 270]
        )
        #expect(shares[10] == 0.3)
        #expect(shares[20] == 0.7)
    }

    @Test func coalitionRegressionIsDropped() {
        let shares = CoalitionGPUCollector.shares(
            previous: [10: 100, 20: 200],
            current: [10: 90, 20: 250]
        )
        #expect(shares == [20: 1.0])
    }
}
```

- [x] **Step 2: Run GPU tests and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/GPUCollectorTests
```

Expected: compilation fails because GPU collector types do not exist.

- [x] **Step 3: Extend the C bridge with opaque GPU results**

Append these stable structures and functions to `DarwinBridge.h`:

```c
typedef enum {
    CPUA_GPU_SOURCE_UNAVAILABLE = 0,
    CPUA_GPU_SOURCE_IOREPORT = 1,
    CPUA_GPU_SOURCE_IOACCELERATOR = 2
} CPUAGPUSource;

typedef struct {
    double usage;
    CPUAGPUSource source;
} CPUAGPUSample;

typedef struct {
    uint64_t coalition_id;
    uint64_t gpu_time;
} CPUACoalitionGPUCounter;

void *CPUACreateGPUContext(void);
void CPUADestroyGPUContext(void *context);
bool CPUACopyGPUSample(void *context, CPUAGPUSample *output);
bool CPUACopyProcessCoalitionID(pid_t pid, uint64_t *output);
bool CPUACopyCoalitionGPUCounter(uint64_t coalition_id, CPUACoalitionGPUCounter *output);
```

- [x] **Step 4: Implement runtime symbol loading without link-time private dependencies**

In `DarwinBridge.c`, use `dlopen` and `dlsym` for the complete required IOReport symbol set:

```text
IOReportCopyChannelsInGroup
IOReportCreateSubscription
IOReportCreateSamples
IOReportCreateSamplesDelta
IOReportChannelGetChannelName
IOReportChannelGetGroup
IOReportStateGetCount
IOReportStateGetNameForIndex
IOReportStateGetResidency
```

Load the IOKit image at runtime, require every symbol before constructing a context, and return `NULL` if any symbol is missing. Subscribe only to GPU Stats and GPU Performance States channels. Keep the previous sample inside the context, compute deltas, sum residency states whose names contain `active`, and divide by total residency. Validate `isfinite`, `total > 0`, and `0 <= usage <= 1` before exposing a result.

If IOReport setup or sampling fails, query `IOAccelerator` services for a numeric `PerformanceStatistics` device-utilization key. Return `CPUA_GPU_SOURCE_UNAVAILABLE` when neither source returns a finite value. Never crash, trap, or retain a partially initialized private context.

Call public `proc_pidinfo` directly with the private coalition selector, and dynamically load the exported-but-private coalition usage function:

```text
coalition_info_resource_usage
```

Read `COALITION_TYPE_RESOURCE` from `proc_pidcoalitioninfo`, then copy `gpu_time` from `coalition_resource_usage`. Return false for absent, zero, or malformed coalition IDs. `CPUACreateGPUContext` must still allocate a context when IOReport is unavailable but the IOAccelerator fallback can be queried; return `NULL` only when allocation itself fails.

- [x] **Step 5: Implement Swift aggregation and attribution**

Create `GPUCollectors.swift` with these pure operations and actor boundaries:

```swift
import AppKit
import Foundation

struct GPUResidency: Equatable, Sendable {
    let active: UInt64
    let total: UInt64
}

actor SystemGPUCollector: GPUCollecting {
    private let context: UnsafeMutableRawPointer?
    private var coalitionCollector = CoalitionGPUCollector()

    init() {
        context = CPUACreateGPUContext()
    }

    deinit {
        if let context { CPUADestroyGPUContext(context) }
    }

    static func aggregate(_ rows: [GPUResidency]) -> Double? {
        let active = rows.reduce(UInt64(0)) { $0 &+ $1.active }
        let total = rows.reduce(UInt64(0)) { $0 &+ $1.total }
        guard total > 0, active <= total else { return nil }
        return Double(active) / Double(total)
    }

    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource) {
        guard let context else { return (nil, .unavailable) }
        var sample = CPUAGPUSample()
        guard CPUACopyGPUSample(context, &sample), sample.usage.isFinite else {
            return (nil, .unavailable)
        }
        let source: GPUSource = sample.source == CPUA_GPU_SOURCE_IOREPORT
            ? .ioReport
            : sample.source == CPUA_GPU_SOURCE_IOACCELERATOR
                ? .ioAccelerator
                : .unavailable
        return source == .unavailable
            ? (nil, .unavailable)
            : (max(0, min(sample.usage, 1)), source)
    }

    func sampleGroups() async throws -> [GPUGroupMetric] {
        await coalitionCollector.sample()
    }
}

private struct CoalitionMember: Sendable {
    let coalitionID: UInt64
    let identity: ProcessIdentity
    let name: String
    let isApplication: Bool
}

actor CoalitionGPUCollector {
    private var previous: [UInt64: UInt64] = [:]

    static func shares(
        previous: [UInt64: UInt64],
        current: [UInt64: UInt64]
    ) -> [UInt64: Double] {
        var deltas: [UInt64: UInt64] = [:]
        for (id, value) in current {
            guard let old = previous[id], value >= old else { continue }
            deltas[id] = value - old
        }
        let total = deltas.values.reduce(0, +)
        guard total > 0 else { return [:] }
        return deltas.mapValues { Double($0) / Double(total) }
    }

    func sample() async -> [GPUGroupMetric] {
        let members = await Self.copyMembers()
        let grouped = Dictionary(grouping: members, by: \.coalitionID)
        var current: [UInt64: UInt64] = [:]
        for coalitionID in grouped.keys {
            var counter = CPUACoalitionGPUCounter()
            if CPUACopyCoalitionGPUCounter(coalitionID, &counter) {
                current[coalitionID] = counter.gpu_time
            }
        }

        let activityShares = Self.shares(previous: previous, current: current)
        previous = current

        return activityShares.compactMap { coalitionID, share in
            guard let groupMembers = grouped[coalitionID], !groupMembers.isEmpty else {
                return nil
            }
            let oldest = groupMembers.min {
                $0.identity.startTimeNanoseconds < $1.identity.startTimeNanoseconds
            }
            let leader = groupMembers
                .filter(\.isApplication)
                .min { $0.identity.startTimeNanoseconds < $1.identity.startTimeNanoseconds }
            return GPUGroupMetric(
                id: coalitionID,
                name: leader?.name ?? oldest?.name ?? "Process group",
                leader: leader?.identity,
                members: groupMembers.map(\.identity),
                activityShare: share
            )
        }
        .sorted { $0.activityShare > $1.activityShare }
        .prefix(10)
        .map { $0 }
    }

    @MainActor
    private static func copyMembers() -> [CoalitionMember] {
        let requiredBytes = CPUACopyAllPIDs(nil, 0)
        guard requiredBytes > 0 else { return [] }
        var pids = [pid_t](
            repeating: 0,
            count: Int(requiredBytes) / MemoryLayout<pid_t>.stride
        )
        let filledBytes = pids.withUnsafeMutableBytes { bytes in
            CPUACopyAllPIDs(
                bytes.bindMemory(to: pid_t.self).baseAddress,
                Int32(bytes.count)
            )
        }
        guard filledBytes > 0 else { return [] }

        return pids.prefix(Int(filledBytes) / MemoryLayout<pid_t>.stride).compactMap { pid in
            guard pid > 0 else { return nil }
            var process = CPUAProcessCounter()
            var coalitionID: UInt64 = 0
            guard CPUACopyProcessCounter(pid, &process),
                  CPUACopyProcessCoalitionID(pid, &coalitionID),
                  coalitionID > 0 else { return nil }
            let running = NSRunningApplication(processIdentifier: pid)
            return CoalitionMember(
                coalitionID: coalitionID,
                identity: ProcessIdentity(
                    pid: pid,
                    startTimeNanoseconds: process.start_time_ns
                ),
                name: running?.localizedName ?? "PID \(pid)",
                isApplication: running?.activationPolicy == .regular
            )
        }
    }
}
```

Import AppKit for `NSRunningApplication`. Keep AppKit objects on the main actor and return only immutable Sendable member values to the collector actor.

- [x] **Step 6: Add deterministic IOReport fixtures**

Store plist fixtures containing channel name, die identifier, state name, previous residency, and current residency. Include one single-die case and one two-die Ultra case. Parse them only in tests and assert weighted aggregation; production code must not depend on fixture names or exact chip-generation labels.

- [x] **Step 7: Run tests and validate degradation**

Run the Step 2 command again.

Expected: four pure tests and both fixture tests pass. On the development Mac, a smoke sample returns either a finite `0...1` utilization with `.ioReport`/`.ioAccelerator`, or `(nil, .unavailable)` without affecting the CPU collector.

- [x] **Step 8: Commit the optional GPU adapter**

```bash
git add CPUAlertApp/Monitoring/DarwinBridge.h CPUAlertApp/Monitoring/DarwinBridge.c CPUAlertApp/Monitoring/GPUCollectors.swift CPUAlertTests/GPUCollectorTests.swift CPUAlertTests/Fixtures
git commit -m "feat: add fail-closed GPU monitoring"
```

---

### Task 5: Unified Sampling Engine and Monitor Panel

**Files:**
- Create: `CPUAlertApp/Monitoring/SamplingEngine.swift`
- Create: `CPUAlertApp/Monitoring/PowerStateMonitor.swift`
- Create: `CPUAlertApp/App/MonitorModel.swift`
- Create: `CPUAlertApp/UI/MonitorPanel.swift`
- Create: `CPUAlertApp/UI/RankedProcessList.swift`
- Modify: `CPUAlertApp/App/CPUAlertApp.swift`
- Modify: `CPUAlertApp/UI/MenuBarLabel.swift`
- Create: `CPUAlertTests/SamplingEngineTests.swift`

**Interfaces:**
- Consumes: Collector protocols, metric types, thresholds, and cadence policy from Tasks 2-4.
- Produces: `SamplingEngine.snapshots(context:) -> AsyncStream<MetricsSnapshot>` and `MonitorModel`, the only metric state read by SwiftUI.

- [x] **Step 1: Write a failing single-cycle engine test with fakes**

Create `SamplingEngineTests.swift`:

```swift
import Foundation
import Testing
@testable import CPUAlert

private actor FakeSystemCPU: SystemCPUCollecting {
    func sampleSystemCPU() async throws -> Double? { 0.72 }
}

private actor FakeProcesses: ProcessCPUCollecting {
    func sampleProcesses() async throws -> [ProcessMetric] { [] }
    func sampleThreads(for process: ProcessIdentity) async throws -> [ThreadMetric] { [] }
}

private actor FakeGPU: GPUCollecting {
    func sampleSystemGPU() async throws -> (usage: Double?, source: GPUSource) {
        (0.18, .ioReport)
    }
    func sampleGroups() async throws -> [GPUGroupMetric] { [] }
}

struct SamplingEngineTests {
    @Test func cyclePublishesIndependentPressureLevels() async {
        let engine = SamplingEngine(
            systemCPU: FakeSystemCPU(),
            processes: FakeProcesses(),
            gpu: FakeGPU(),
            thresholds: .defaults
        )
        let snapshot = await engine.collectOnce(
            context: .closedGreen,
            includeRankings: true,
            now: Date(timeIntervalSince1970: 1)
        )
        #expect(snapshot.cpuUsage == 0.72)
        #expect(snapshot.gpuUsage == 0.18)
        #expect(snapshot.cpuLevel == .yellow)
        #expect(snapshot.gpuLevel == .green)
    }
}
```

- [x] **Step 2: Run the engine test and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/SamplingEngineTests
```

Expected: compilation fails because `SamplingEngine` does not exist.

- [x] **Step 3: Implement one-cycle collection and cached rankings**

Create `SamplingEngine.swift` with an actor that owns collectors, previous pressure levels, the last successful rankings, and a single running task. Implement this exact entry surface:

```swift
actor SamplingEngine {
    typealias ContextProvider = @Sendable () async -> SamplingContext

    init(
        systemCPU: any SystemCPUCollecting,
        processes: any ProcessCPUCollecting,
        gpu: any GPUCollecting,
        thresholds: AlertThresholds
    )

    func collectOnce(
        context: SamplingContext,
        includeRankings: Bool,
        now: Date
    ) async -> MetricsSnapshot

    func updateThresholds(_ thresholds: AlertThresholds)

    func snapshots(
        context: @escaping ContextProvider
    ) -> AsyncStream<MetricsSnapshot>

    func stop()
}
```

In `collectOnce`, run system CPU and system GPU concurrently with `async let`. Run process and coalition ranking concurrently only when `includeRankings` is true; otherwise reuse the last arrays. Read threads only when `context.expandedProcess` is non-nil and rankings are included. Convert individual collector errors to unavailable/empty values, never terminate the loop, and compute CPU/GPU pressure independently with `AlertThresholds.level`.

In `snapshots`, create exactly one task. On each iteration, obtain context, derive cadence, determine whether the monotonic time has reached the ranking deadline, yield one snapshot, and sleep for the system cadence. Use `ContinuousClock` for scheduling and `Date` only for display timestamps. Cancel the task and finish the continuation in `stop()` and `onTermination`.

- [x] **Step 4: Run the engine test**

Run the Step 2 command again.

Expected: the test passes with independent yellow CPU and green GPU levels.

- [x] **Step 5: Add the main-actor presentation model**

Before the presentation model, create `PowerStateMonitor.swift`. Link IOKit and use `IOPSCopyPowerSourcesInfo`, `IOPSCopyPowerSourcesList`, and `IOPSGetPowerSourceDescription` to report `lowBattery = true` when the internal battery is not charging and its current/max capacity is at or below 20%, or when `ProcessInfo.processInfo.isLowPowerModeEnabled` is true. Refresh from the IOPowerSources notification run-loop source and `NSProcessInfoPowerStateDidChange`; do not poll from a SwiftUI view.

Create `MonitorModel.swift`:

```swift
import Observation
import SwiftUI

@MainActor
@Observable
final class MonitorModel {
    private(set) var snapshot = MetricsSnapshot.empty
    var selectedResource: ResourceKind = .cpu
    var panelIsOpen = false
    var showTenRows = false
    var expandedProcess: ProcessIdentity?
    private(set) var trend: [MetricsSnapshot] = []

    private let engine: SamplingEngine
    private let powerState: PowerStateMonitor
    private var observationTask: Task<Void, Never>?

    init(engine: SamplingEngine, powerState: PowerStateMonitor) {
        self.engine = engine
        self.powerState = powerState
    }

    func start() {
        guard observationTask == nil else { return }
        observationTask = Task {
            let stream = await engine.snapshots { [weak self] in
                await self?.samplingContext ?? .closedGreen
            }
            for await value in stream {
                guard !Task.isCancelled else { break }
                snapshot = value
                if panelIsOpen {
                    trend.append(value)
                    trend.removeAll { value.sampledAt.timeIntervalSince($0.sampledAt) > 60 }
                } else {
                    trend.removeAll(keepingCapacity: true)
                }
            }
        }
    }

    func stop() {
        observationTask?.cancel()
        observationTask = nil
        Task { await engine.stop() }
    }

    private var samplingContext: SamplingContext {
        SamplingContext(
            panelIsOpen: panelIsOpen,
            lowBattery: powerState.lowBattery,
            cpuLevel: snapshot.cpuLevel,
            gpuLevel: snapshot.gpuLevel,
            expandedProcess: expandedProcess
        )
    }
}
```

- [x] **Step 6: Build the compact panel**

Implement `MonitorPanel` as a fixed 360-point-wide layout with these sections in order: combined CPU/GPU header, 60-second sparkline while open, CPU/GPU segmented picker, top-5/top-10 control, ranked list, and a footer containing Settings and Quit. Use `LazyVStack`, no timers in views, and no hidden animations when the panel is closed.

Implement `RankedProcessList` so CPU rows show app icon, process name, PID, and whole-machine percentage. Expanded CPU rows show the current top thread rows. GPU rows show group name, member count, and the localized label “GPU activity share”; they must not display “GPU usage”.

Update `MenuBarLabel` to derive colors from `snapshot.cpuLevel` and `snapshot.gpuLevel`, retain the last integer percentages, and render unavailable GPU in secondary gray as `GPU —`.

- [x] **Step 7: Compose production dependencies once**

In `CPUAlertApp`, construct one `SamplingEngine`, one `PowerStateMonitor`, one `MonitorModel`, and inject that model into both the menu label and panel. Attach `.task { model.start() }` to the menu-bar label so sampling starts when the status item mounts rather than waiting for the panel to open; the guard in `start()` prevents duplicates. Set `panelIsOpen` from panel appearance/disappearance and call `stop()` during application termination.

- [x] **Step 8: Run tests and manual UI checks**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64'
```

Expected: all tests pass. Manually confirm the panel does not steal focus unexpectedly, switching resources does not restart collectors, thread rows disappear when collapsed, and closing the panel clears trend history and returns to background cadence.

- [x] **Step 9: Commit the working monitor**

```bash
git add CPUAlertApp/App CPUAlertApp/Monitoring/SamplingEngine.swift CPUAlertApp/Monitoring/PowerStateMonitor.swift CPUAlertApp/UI CPUAlertTests/SamplingEngineTests.swift
git commit -m "feat: display live CPU and GPU rankings"
```

---

### Task 6: Sustained Alerts and Notification Delivery

**Files:**
- Create: `CPUAlertApp/Alerts/AlertEngine.swift`
- Create: `CPUAlertApp/Alerts/NotificationService.swift`
- Create: `CPUAlertTests/AlertEngineTests.swift`
- Modify: `CPUAlertApp/App/MonitorModel.swift`
- Modify: `CPUAlertApp/Info.plist`

**Interfaces:**
- Consumes: `MetricsSnapshot`, `ResourceKind`, and `PressureLevel`.
- Produces: `AlertEngine.evaluate(resource:level:elapsed:) -> [AlertTrigger]` and `NotificationService.enqueue(_:snapshot:)`.

- [ ] **Step 1: Write failing alert timing tests**

Create `AlertEngineTests.swift` using explicit monotonic durations:

```swift
import Testing
@testable import CPUAlert

struct AlertEngineTests {
    @Test func yellowRequiresFifteenSeconds() {
        var engine = AlertEngine()
        #expect(engine.evaluate(resource: .cpu, level: .yellow, elapsed: .seconds(0)).isEmpty)
        #expect(engine.evaluate(resource: .cpu, level: .yellow, elapsed: .seconds(14)).isEmpty)
        #expect(engine.evaluate(resource: .cpu, level: .yellow, elapsed: .seconds(15)) == [
            AlertTrigger(resource: .cpu, level: .yellow)
        ])
        #expect(engine.evaluate(resource: .cpu, level: .yellow, elapsed: .seconds(16)).isEmpty)
    }

    @Test func orangeAndRedUseApprovedDurations() {
        var orange = AlertEngine()
        #expect(orange.evaluate(resource: .gpu, level: .orange, elapsed: .seconds(0)).isEmpty)
        #expect(orange.evaluate(resource: .gpu, level: .orange, elapsed: .seconds(9)).isEmpty)
        #expect(orange.evaluate(resource: .gpu, level: .orange, elapsed: .seconds(10)).count == 1)

        var red = AlertEngine()
        #expect(red.evaluate(resource: .cpu, level: .red, elapsed: .seconds(0)).isEmpty)
        #expect(red.evaluate(resource: .cpu, level: .red, elapsed: .seconds(4)).isEmpty)
        #expect(red.evaluate(resource: .cpu, level: .red, elapsed: .seconds(5)).count == 1)
        #expect(red.evaluate(resource: .cpu, level: .red, elapsed: .seconds(604)).isEmpty)
        #expect(red.evaluate(resource: .cpu, level: .red, elapsed: .seconds(605)).count == 1)
    }

    @Test func unavailableResetsPendingAlert() {
        var engine = AlertEngine()
        _ = engine.evaluate(resource: .gpu, level: .yellow, elapsed: .seconds(10))
        #expect(engine.evaluate(resource: .gpu, level: .unavailable, elapsed: .seconds(11)).isEmpty)
        #expect(engine.evaluate(resource: .gpu, level: .yellow, elapsed: .seconds(15)).isEmpty)
    }
}
```

- [ ] **Step 2: Run alert tests and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/AlertEngineTests
```

Expected: compilation fails because the alert types do not exist.

- [ ] **Step 3: Implement the per-resource state machine**

Create `AlertEngine.swift`:

```swift
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
```

- [ ] **Step 4: Run the timing tests**

Run the Step 2 command again.

Expected: all timing tests pass with elapsed values interpreted as absolute monotonic offsets and `enteredAt` set only on actual level entry.

- [ ] **Step 5: Implement notification authorization and merging**

Create `NotificationService` as an actor wrapping `UNUserNotificationCenter`. Provide:

```swift
actor NotificationService {
    func requestAuthorization() async -> Bool
    func enqueue(_ triggers: [AlertTrigger], snapshot: MetricsSnapshot) async
}
```

Queue triggers for a 2-second merge window. If CPU and GPU triggers are pending, send one notification titled “CPUAlert: High system load” with both percentages. Otherwise name the resource and level. Include only the current top process/group name in the body. Set no destructive notification action and persist no process name after delivery.

Treat denied permission as a normal result: cache authorization state, skip future delivery attempts, and leave menu colors unchanged.

- [ ] **Step 6: Feed snapshots into alerts**

In `MonitorModel`, own one `AlertEngine`, one `NotificationService`, and a start-time `ContinuousClock.Instant`. After assigning each snapshot, evaluate CPU and GPU against the same elapsed duration and pass the combined trigger array to `NotificationService.enqueue`.

- [ ] **Step 7: Run all tests and commit**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64'
git add CPUAlertApp/Alerts CPUAlertApp/App/MonitorModel.swift CPUAlertApp/Info.plist CPUAlertTests/AlertEngineTests.swift
git commit -m "feat: notify on sustained resource pressure"
```

Expected: tests pass; no notification appears before its sustained-duration threshold; denied notification access does not log repeatedly or stop sampling.

---

### Task 7: Safe Process Termination and On-Demand Root Helper

**Files:**
- Create: `CPUAlertShared/HelperProtocol.swift`
- Create: `CPUAlertShared/ProcessIdentityReader.swift`
- Create: `CPUAlertApp/Termination/ProtectedProcessPolicy.swift`
- Create: `CPUAlertApp/Termination/TerminationCoordinator.swift`
- Create: `CPUAlertApp/Termination/HelperClient.swift`
- Create: `CPUAlertApp/Termination/LegacyBlessingInstaller.h`
- Create: `CPUAlertApp/Termination/LegacyBlessingInstaller.m`
- Create: `CPUAlertHelper/main.swift`
- Create: `CPUAlertHelper/HelperService.swift`
- Create: `CPUAlertHelper/Helper-Info.plist`
- Create: `CPUAlertHelper/Launchd.plist`
- Create: `CPUAlertTests/ProtectedProcessPolicyTests.swift`
- Create: `CPUAlertTests/TerminationCoordinatorTests.swift`
- Modify: `CPUAlertApp/Info.plist`
- Modify: `CPUAlert.xcodeproj/project.pbxproj`

**Interfaces:**
- Consumes: `ProcessIdentity`, ranked CPU/GPU rows, and app bundle identity.
- Produces: `TerminationCoordinator.requestGraceful(_:)`, `TerminationCoordinator.requestForce(_:)`, signed XPC service `com.cpualert.helper.xpc`, and fixed helper operations `.terminate` and `.uninstall`.

- [ ] **Step 1: Write protected-process and PID-reuse tests**

Create `ProtectedProcessPolicyTests.swift`:

```swift
import Testing
@testable import CPUAlert

struct ProtectedProcessPolicyTests {
    @Test func protectedProcessesAreDenied() {
        let rows: [(Int32, String)] = [
            (0, "kernel_task"),
            (1, "launchd"),
            (88, "WindowServer"),
            (99, "loginwindow"),
            (100, "CPUAlert"),
            (101, "com.cpualert.helper")
        ]
        for (pid, name) in rows {
            #expect(ProtectedProcessPolicy.isProtected(pid: pid, name: name))
        }
    }

    @Test func ordinaryChildIsAllowed() {
        #expect(!ProtectedProcessPolicy.isProtected(pid: 4_242, name: "CPUStress"))
    }
}
```

Create coordinator tests using a fake identity reader and fake signal sender. Assert that a changed start time is rejected, graceful termination sends only signal 15, and force termination sends signal 9 only after an explicit force method call.

- [ ] **Step 2: Run termination tests and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/ProtectedProcessPolicyTests \
  -only-testing:CPUAlertTests/TerminationCoordinatorTests
```

Expected: compilation fails because termination types do not exist.

- [ ] **Step 3: Define the fixed secure-coding protocol**

Create `HelperProtocol.swift` and compile it into both app and helper targets:

```swift
import Foundation

@objc enum HelperOperation: Int {
    case terminate = 1
    case uninstall = 2
}

@objc final class HelperRequest: NSObject, NSSecureCoding, @unchecked Sendable {
    static var supportsSecureCoding: Bool { true }

    let operation: HelperOperation
    let pid: Int32
    let startTimeNanoseconds: UInt64
    let signal: Int32

    init(operation: HelperOperation, pid: Int32, startTimeNanoseconds: UInt64, signal: Int32) {
        self.operation = operation
        self.pid = pid
        self.startTimeNanoseconds = startTimeNanoseconds
        self.signal = signal
    }

    required init?(coder: NSCoder) {
        guard let operation = HelperOperation(rawValue: coder.decodeInteger(forKey: "operation")) else {
            return nil
        }
        self.operation = operation
        pid = Int32(coder.decodeInt32(forKey: "pid"))
        startTimeNanoseconds = UInt64(bitPattern: coder.decodeInt64(forKey: "startTime"))
        signal = Int32(coder.decodeInt32(forKey: "signal"))
    }

    func encode(with coder: NSCoder) {
        coder.encode(operation.rawValue, forKey: "operation")
        coder.encode(pid, forKey: "pid")
        coder.encode(Int64(bitPattern: startTimeNanoseconds), forKey: "startTime")
        coder.encode(signal, forKey: "signal")
    }
}

@objc final class HelperResponse: NSObject, NSSecureCoding, @unchecked Sendable {
    static var supportsSecureCoding: Bool { true }
    let errorCode: Int32

    init(errorCode: Int32) { self.errorCode = errorCode }

    required init?(coder: NSCoder) {
        errorCode = coder.decodeInt32(forKey: "errorCode")
    }

    func encode(with coder: NSCoder) {
        coder.encode(errorCode, forKey: "errorCode")
    }
}

@objc protocol HelperXPCProtocol {
    func perform(_ request: HelperRequest, withReply reply: @escaping (HelperResponse) -> Void)
}
```

- [ ] **Step 4: Centralize identity and protected-process checks**

Implement `ProcessIdentityReader.currentIdentity(pid:)` using `PROC_PIDTBSDINFO` and the same start-time conversion as Task 3. It must return the current name, executable path, UID, and `ProcessIdentity` in one value.

Implement `ProtectedProcessPolicy.isProtected(pid:name:)` as a pure function. Reject PID `<= 1`, exact protected names, names beginning with `CPUAlert` or `com.cpualert`, and the main app/helper's own current PIDs. Compile the same policy source into both targets so the helper never trusts a UI-only decision.

- [ ] **Step 5: Implement same-user termination first**

In `TerminationCoordinator`, re-read identity immediately before every action. If identity differs from the selected row, return `.identityChanged`. If the target UID equals `getuid()`, terminate a regular GUI app with `NSRunningApplication.terminate()` and otherwise call `kill(pid, SIGTERM)`.

After a successful graceful request, sleep 3 seconds on a task and re-read the same identity. Return `.forceAvailable` only when it remains alive with the same start time. `requestForce(_:)` must be a separate method called only after the confirmation sheet and must repeat identity/protection checks before sending `SIGKILL`.

For a GPU group, target only its explicit application leader. If `GPUGroupMetric.leader` is nil, present the member processes for a single selection. Never submit multiple member PIDs from one action and never interpret a coalition ID as a process ID.

- [ ] **Step 6: Isolate deprecated helper installation**

Use an Objective-C adapter because Swift's availability diagnostics around removed ServiceManagement declarations are harder to contain. `LegacyBlessingInstaller.m` must expose only:

```objc
- (BOOL)installWithAuthorization:(AuthorizationRef)authorization
                           error:(NSError **)error;
- (BOOL)removeJobWithAuthorization:(AuthorizationRef)authorization
                              error:(NSError **)error;
```

Call `SMJobBless(kSMDomainSystemLaunchd, CFSTR("com.cpualert.helper"), ...)` for installation and `SMJobRemove(..., true, ...)` for removal. Suppress deprecation warnings only around those calls.

The SDK contract says `SMJobRemove` removes the registered job; it does not promise removal of copied files. Therefore, the app's removal flow must first send the authenticated fixed `.uninstall` operation, which deletes only these exact paths, then invoke `SMJobRemove` to clear launchd's registration:

```text
/Library/PrivilegedHelperTools/com.cpualert.helper
/Library/LaunchDaemons/com.cpualert.helper.plist
```

Make both steps idempotent and report partial cleanup explicitly.

- [ ] **Step 7: Configure mutual code-signing requirements**

Add `SMPrivilegedExecutables` to the app plist with key `com.cpualert.helper` and a designated requirement containing the exact helper identifier plus the configured Team ID.

Add `SMAuthorizedClients` to `Helper-Info.plist` with the exact app identifier plus the same Team ID. Embed `Helper-Info.plist` and `Launchd.plist` in the helper's `__TEXT,__info_plist` and `__TEXT,__launchd_plist` sections using `-sectcreate` linker flags. Place the built helper at `CPUAlert.app/Contents/Library/LaunchServices/com.cpualert.helper`.

Use this launchd payload:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.cpualert.helper</string>
    <key>MachServices</key>
    <dict>
        <key>com.cpualert.helper.xpc</key>
        <true/>
    </dict>
    <key>KeepAlive</key>
    <false/>
</dict>
</plist>
```

- [ ] **Step 8: Validate every XPC caller in the helper**

In `NSXPCListenerDelegate.listener(_:shouldAcceptNewConnection:)`, obtain the connection audit token and create a `SecCode` guest. Require the app identifier `com.cpualert.app`, the configured Team ID, a valid signature, and a designated requirement match. Reject the connection before assigning an exported object if any check fails.

Configure `NSXPCInterface` in both app and helper with `HelperRequest` as the only request class and `HelperResponse` as the only reply class for `perform(_:withReply:)`. Install interruption and invalidation handlers, clear the connection on either callback, and convert connection loss to a typed failure rather than retrying a destructive operation automatically.

For `.terminate`, allow signals 15 and 9 only. Re-read PID identity, compare start time, apply `ProtectedProcessPolicy`, and call `kill`. Record the monotonic time and `ProcessIdentity` after a successful signal 15. Reject signal 9 unless the same identity received signal 15 from an accepted app connection at least 3 seconds earlier and still exists; remove the record after use or identity change. Return `errno` in `HelperResponse` without translating it to arbitrary text.

For `.uninstall`, require PID and signal fields to be zero, unlink only the two fixed paths, return the first non-`ENOENT` error, and exit after replying. Maintain a 15-second idle timer that exits when no XPC connection or request is active.

- [ ] **Step 9: Require local authentication for every Root operation**

Before connecting to the helper for a Root-owned target, create a fresh `LAContext` and evaluate `.deviceOwnerAuthentication` with localized reason “CPUAlert needs permission to terminate a Root process.” Do not cache successful authentication. On cancellation or failure, send no XPC request.

Install the helper only on the first authenticated Root action. If signing or blessing fails, leave ordinary same-user termination enabled and show a non-destructive error with a link to the privilege settings section.

- [ ] **Step 10: Verify only against disposable children**

Run unit tests, launch `CPUStress` as the current user, and verify graceful and forced flows against that fixture. For Root integration, run a separately built fixture only on the development machine and confirm PID/start-time matching before signal delivery. Never use `launchd`, `WindowServer`, `loginwindow`, or another real system service as a test target.

Run:

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/ProtectedProcessPolicyTests \
  -only-testing:CPUAlertTests/TerminationCoordinatorTests
xcodebuild build \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -configuration Debug \
  -derivedDataPath build/DerivedData
codesign --verify --deep --strict --verbose=2 \
  build/DerivedData/Build/Products/Debug/CPUAlert.app
```

Expected: tests and signature verification pass; helper is absent while idle unless installed, and exits within 15 seconds after use.

- [ ] **Step 11: Commit the termination boundary**

```bash
git add CPUAlert.xcodeproj CPUAlertApp/Info.plist CPUAlertApp/Termination CPUAlertShared CPUAlertHelper CPUAlertTests/ProtectedProcessPolicyTests.swift CPUAlertTests/TerminationCoordinatorTests.swift
git commit -m "feat: add authenticated process termination"
```

---

### Task 8: Settings, First Run, Localization, and Accessibility

**Files:**
- Create: `CPUAlertApp/Settings/AppSettings.swift`
- Create: `CPUAlertApp/Settings/LoginItemService.swift`
- Create: `CPUAlertApp/UI/FirstRunView.swift`
- Create: `CPUAlertApp/UI/SettingsView.swift`
- Create: `CPUAlertApp/Resources/Localizable.xcstrings`
- Create: `CPUAlertTests/AppSettingsTests.swift`
- Create: `CPUAlertUITests/CPUAlertUITests.swift`
- Modify: `CPUAlertApp/App/CPUAlertApp.swift`
- Modify: `CPUAlertApp/App/MonitorModel.swift`
- Modify: `CPUAlertApp/UI/MonitorPanel.swift`
- Modify: `CPUAlertApp/UI/RankedProcessList.swift`

**Interfaces:**
- Consumes: Alert thresholds, sampling state, notification service, helper client, and production panel.
- Produces: Validated local settings, login-item control, bilingual visible copy, first-run workflow, and deterministic UI-test launch mode.

- [ ] **Step 1: Write settings validation tests**

Create `AppSettingsTests.swift`:

```swift
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
            yellow: 0.65, orange: 0.80, red: 0.95
        ))
    }
}
```

- [ ] **Step 2: Run settings tests and verify failure**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -only-testing:CPUAlertTests/AppSettingsTests
```

Expected: compilation fails because settings types do not exist.

- [ ] **Step 3: Implement validated local preferences**

Define a small `SettingsStore` protocol with `double`, `bool`, and setter methods. Make `UserDefaults` the production implementation and `InMemorySettingsStore` the test implementation. `AppSettings` must expose:

```swift
@MainActor
@Observable
final class AppSettings {
    private(set) var thresholds: AlertThresholds
    var notificationsEnabled: Bool
    var launchAtLogin: Bool
    var hasCompletedFirstRun: Bool

    @discardableResult
    func setThresholds(yellow: Double, orange: Double, red: Double) -> Bool
}
```

Store only thresholds, permission preferences, login preference, and first-run completion. Do not store snapshots, process names, bundle identifiers, PIDs, or alert history.

After `setThresholds` succeeds, call `SamplingEngine.updateThresholds(_:)` so menu colors, cadence escalation, and subsequent snapshots use the new values without restarting the engine. Invalid edits remain visible as validation feedback but never reach the engine or persistent store.

- [ ] **Step 4: Implement login-at-launch safely**

Create `LoginItemService` around `SMAppService.mainApp`. Map `.enabled`, `.requiresApproval`, `.notRegistered`, and `.notFound` into a small display enum. Register or unregister only after a direct user toggle. For `.requiresApproval`, present a button that opens System Settings' Login Items page rather than repeatedly registering.

- [ ] **Step 5: Implement in-panel first run**

At the top of `MonitorPanel`, show `FirstRunView` until completed. Provide two explicit controls: “Allow notifications” and “Launch at login.” A “Not now” button completes onboarding without either permission. Request notification authorization only from its button and register the login item only from its toggle.

- [ ] **Step 6: Build four focused Settings sections**

Create a 420-point-wide Settings view with:

| Section | Controls |
|---|---|
| General | Launch at login, show 5/10 rows, reset first-run prompts. |
| Alerts | Yellow/orange/red percentage sliders constrained to valid 5-point gaps; notification status. |
| Privilege | Helper installed state, install/remove action, legacy-helper warning. |
| Diagnostics | Active cadence, GPU source, latest collector durations, no export or upload button. |

Removing the helper must require local authentication and invoke the fixed uninstall sequence from Task 7.

- [ ] **Step 7: Add complete English and Simplified Chinese catalog entries**

Use stable semantic keys, including:

```text
menu.cpu.format
menu.gpu.format
menu.gpu.unavailable
panel.cpu
panel.gpu
panel.gpu.activityShare
panel.thread
action.terminate
action.forceTerminate
action.settings
alert.yellow.title
alert.orange.title
alert.red.title
onboarding.notifications
onboarding.loginItem
settings.privilege.legacyWarning
```

Translate “GPU activity share” as “GPU 活动占比”; do not translate it as per-process “GPU 占用率”.

- [ ] **Step 8: Add deterministic UI-test launch state**

When launch arguments contain `--ui-testing`, inject fixed snapshots rather than real collectors and skip notification/helper prompts. Provide launch arguments for green, red CPU, unavailable GPU, five rows, ten rows, and expanded threads.

Create UI tests that open the menu extra, switch CPU/GPU, expand one CPU process, verify `GPU —`, open Settings, and navigate all controls with keyboard focus.

- [ ] **Step 9: Verify accessibility and localization**

Run:

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64'
```

Expected: unit and UI tests pass. Manually test English and Simplified Chinese, VoiceOver labels such as “CPU 42 percent, normal”, Full Keyboard Access, Reduce Motion, Increase Contrast, light appearance, and dark appearance. No critical state may be communicated by color alone.

- [ ] **Step 10: Commit user-facing configuration**

```bash
git add CPUAlertApp/Settings CPUAlertApp/UI CPUAlertApp/Resources/Localizable.xcstrings CPUAlertApp/App CPUAlertTests/AppSettingsTests.swift CPUAlertUITests
git commit -m "feat: add settings onboarding and localization"
```

---

### Task 9: Stress Fixtures, Performance Gates, and Operational Documentation

**Files:**
- Create: `TestFixtures/CPUStress/main.swift`
- Create: `TestFixtures/GPUStress/main.swift`
- Create: `TestFixtures/GPUStress/Stress.metal`
- Create: `Scripts/benchmark.sh`
- Create: `README.md`
- Modify: `CPUAlert.xcodeproj/project.pbxproj`

**Interfaces:**
- Consumes: The complete app, helper, and test suite.
- Produces: Reproducible CPU/GPU load, measurable performance evidence, archive verification, and operator documentation.

- [ ] **Step 1: Add a bounded CPU fixture**

Implement `CPUStress` to accept `--workers`, `--duty-percent`, and `--seconds`. Spawn the requested number of tasks; in each 100-millisecond interval, busy-loop for the duty fraction and sleep for the remainder. Clamp workers to `1...activeProcessorCount`, duty to `1...100`, and duration to `1...300`. Exit automatically and handle `SIGTERM`.

Verify:

```bash
xcodebuild build -project CPUAlert.xcodeproj -scheme CPUStress -configuration Debug -derivedDataPath build/DerivedData
build/DerivedData/Build/Products/Debug/CPUStress --workers 1 --duty-percent 50 --seconds 10
```

Expected: the fixture exits after approximately 10 seconds and appears in CPUAlert without exceeding roughly half of one logical CPU divided by the machine's logical CPU count.

- [ ] **Step 2: Add a bounded Metal GPU fixture**

Create `Stress.metal`:

```metal
#include <metal_stdlib>
using namespace metal;

kernel void stressKernel(
    device float *values [[buffer(0)]],
    uint index [[thread_position_in_grid]]
) {
    float value = values[index];
    for (uint iteration = 0; iteration < 4096; ++iteration) {
        value = fma(value, 1.000001f, 0.000001f);
    }
    values[index] = value;
}
```

Implement `GPUStress` with the default Metal device, one compute pipeline, a private buffer, and repeated command buffers for a maximum duration of 60 seconds. Accept `--seconds 1...60`, stop on `SIGTERM`, and never run automatically from the test suite.

Verify:

```bash
xcodebuild build -project CPUAlert.xcodeproj -scheme GPUStress -configuration Debug -derivedDataPath build/DerivedData
build/DerivedData/Build/Products/Debug/GPUStress --seconds 10
```

Expected: the process exits after approximately 10 seconds. CPUAlert shows elevated system GPU when the local IOReport schema is supported and shows the fixture's application group among activity shares when coalition counters are available.

- [ ] **Step 3: Create a repeatable performance script**

Create executable `Scripts/benchmark.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DERIVED="$ROOT/build/DerivedData"
APP="$DERIVED/Build/Products/Release/CPUAlert.app"
TRACE="$ROOT/build/CPUAlert-green.trace"

xcodebuild \
  -project "$ROOT/CPUAlert.xcodeproj" \
  -scheme CPUAlert \
  -configuration Release \
  -derivedDataPath "$DERIVED" \
  build

rm -rf "$TRACE"
xcrun xctrace record \
  --template 'Time Profiler' \
  --time-limit 5m \
  --output "$TRACE" \
  --launch -- "$APP/Contents/MacOS/CPUAlert" --benchmark-green

echo "Trace: $TRACE"
```

The `--benchmark-green` launch mode must disable first-run UI and notifications but use real collectors with the panel closed.

- [ ] **Step 4: Measure all required modes**

Run the script for closed-panel green mode. Repeat with panel-open, elevated CPU, elevated GPU, and expanded-thread launch modes. Record Release-build averages in `README.md` with hardware model, logical CPU count, OS version, Xcode version, sample duration, CPU average, resident memory, and wakeups per second.

Closed-panel green mode passes only when all three values satisfy:

```text
average CPU <= 0.3%
resident memory <= 40 MB
average wakeups <= 1 per second
```

If a gate fails, optimize in this order and rerun the full 5-minute measurement after each change: menu-bar redraw suppression, ranking cadence, IOReport channel filtering, app-icon cache, trend retention, thread sampling. Do not weaken the limits.

- [ ] **Step 5: Run correctness and static-analysis gates**

```bash
xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64'

xcodebuild analyze \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -configuration Release
```

Expected: all tests pass and analysis completes without new warnings. Exercise process churn, sleep/wake, denied notifications, unavailable GPU, helper absence, helper idle exit, and a PID-reuse fake.

- [ ] **Step 6: Document semantics and operational risks**

Write `README.md` with these explicit statements:

- CPU process percentages are normalized against total whole-machine capacity.
- GPU menu usage is best-effort whole-machine utilization; GPU rankings are coalition activity shares and are not direct per-process GPU percentages.
- IOReport and coalition APIs are private/unsupported and may fail after an OS update; CPU monitoring continues with `GPU —`.
- `SMJobBless` is deprecated and selected only for this local development build. Both app and helper still require matching signatures.
- CPUAlert never runs arbitrary privileged commands, never uploads data, and does not retain process history.
- Helper removal uses a fixed authenticated cleanup followed by `SMJobRemove`; manual recovery commands must list only the exact helper and launchd paths.
- Protected system processes cannot be terminated through the app.

Include build instructions, notification/login permission behavior, first-use helper installation, helper removal, known limitations, and the performance table from Step 4.

- [ ] **Step 7: Archive and inspect the final application**

```bash
xcodebuild archive \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -configuration Release \
  -archivePath build/CPUAlert.xcarchive

codesign --verify --deep --strict --verbose=2 \
  build/CPUAlert.xcarchive/Products/Applications/CPUAlert.app

codesign -d -r- --verbose=4 \
  build/CPUAlert.xcarchive/Products/Applications/CPUAlert.app/Contents/Library/LaunchServices/com.cpualert.helper
```

Expected: archive succeeds; deep verification passes; helper designated requirement contains the intended helper identifier and the same Team ID as the app.

- [ ] **Step 8: Commit fixtures and release evidence**

```bash
git add CPUAlert.xcodeproj TestFixtures Scripts README.md
git commit -m "test: add stress and performance gates"
git status --short
```

Expected: the commit succeeds and the worktree is clean.

## Final Acceptance Checklist

- [ ] Menu bar CPU and GPU rows remain fixed-width, colored independently, and legible in light/dark/high-contrast appearances.
- [ ] CPU and GPU continue updating at their expected adaptive cadence without overlapping collection loops.
- [ ] CPU process values use whole-machine normalization; thread values appear only for the expanded process.
- [ ] GPU failure produces gray `GPU —`, no GPU alert, and no CPU disruption.
- [ ] GPU rankings say “activity share” and group by application coalition.
- [ ] Yellow, orange, red, hysteresis, sustained durations, and red cooldown match Global Constraints.
- [ ] Notification denial, helper absence, and unsupported private APIs are recoverable states.
- [ ] `SIGTERM` always precedes an optional confirmed `SIGKILL` for the same PID/start-time identity.
- [ ] Every Root termination authenticates locally and every helper connection passes code-signature validation.
- [ ] Protected process names and PIDs are denied in both app and helper.
- [ ] The helper accepts no path, shell command, environment, or arbitrary executable argument.
- [ ] First-run, Settings, English, Simplified Chinese, keyboard navigation, and VoiceOver flows pass.
- [ ] Release performance meets all three closed-panel green-state limits for five minutes.
- [ ] Full tests, static analysis, archive, and code-signature verification pass.

## Primary References

- Local SDK: `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/mach/task_info.h`
- Local SDK: `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/proc_info.h`
- Local SDK: `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/libproc.h`
- Local SDK: `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/System/Library/Frameworks/ServiceManagement.framework/Headers/ServiceManagement.h`
- Local SDK: `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/System/Library/Frameworks/ServiceManagement.framework/Headers/SMAppService.h`
- Local SDK: `/Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/System/Library/Frameworks/Foundation.framework/Headers/NSXPCConnection.h`
- XNU coalition definitions: `https://github.com/apple-oss-distributions/xnu`
- macmon implementation research: `https://github.com/vladkens/macmon`
- Stats implementation research: `https://github.com/exelban/stats`
- CodexBar menu-bar implementation research: `https://github.com/steipete/CodexBar`

Use reference projects only to confirm behavior and naming. Do not copy source without first checking and complying with its license.
