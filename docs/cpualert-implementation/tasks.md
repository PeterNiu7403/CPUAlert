# CPUAlert Implementation Progress

> This file is the execution source of truth. Update it immediately after each verified task.

## Progress

| Metric | Value |
|---|---:|
| Total tasks | 9 |
| Completed | 8 (88.9%) |
| Remaining | 1 (11.1%) |
| Last updated | 2026-07-19 14:04 CST |

## Tasks

- [x] 1. Native project and menu bar rendering gate
  - Completed: 2026-07-19 12:11 CST
  - Output: native six-target Xcode project, shared build configuration, agent app plist, fixed-width `MenuBarLabel`, signed Debug app, and evidence-driven `NSStatusItem` fallback.
  - Verification: `plutil` passed; all six targets are listed; arm64 macOS Debug build succeeded under Swift 6; strict `codesign` verification passed; real-screen capture showed both CPU and GPU rows after the fallback.
- [x] 2. Metric domain and adaptive sampling policy
  - Completed: 2026-07-19 12:16 CST
  - Output: immutable metric value types, validated alert thresholds, sampling context/cadence, collector protocols, and pure sampling policy.
  - Verification: focused test first failed on missing `AlertThresholds`, then passed 3 Swift Testing cases covering exact thresholds, five-point hysteresis, visibility, elevated pressure, and low battery.
- [x] 3. Whole-machine, process, and thread CPU collectors
  - Completed: 2026-07-19 12:22 CST
  - Output: stable C bridge over Mach/libproc, normalized system and process delta collectors, PID-start-time baselines, app metadata decoration, cached top rankings, and on-demand thread sampling.
  - Verification: focused suite first failed on missing collector types, then passed 4 tests including live system/process/thread smoke sampling; the xcresult summary reports 4/4 passed on arm64 macOS.
- [x] 4. Fail-closed GPU utilization and coalition attribution
  - Completed: 2026-07-19 12:37 CST
  - Output: dynamically loaded IOReport adapter with IOAccelerator fallback, explicit unavailable semantics, overflow-safe weighted residency aggregation, resource-coalition GPU attribution, and deterministic single-/multi-die fixtures.
  - Verification: focused suite first failed because GPU types were absent, then the signed test build succeeded and the xcresult summary reported 7/7 passing tests, including current-hardware live sampling and fail-closed range checks.
- [x] 5. Unified sampling engine and monitor panel
  - Completed: 2026-07-19 12:59 CST
  - Output: resilient single-task sampling stream, adaptive ranking cache, power-state notifications, one main-actor presentation model, fixed-width live panel, CPU thread expansion, GPU coalition ranking, sparkline, and model-driven menu label.
  - Verification: engine test first failed on the missing type; the default signed scheme then passed 15/15 tests. A Debug-only `--open-panel` harness plus real Computer Use inspection verified sustained CPU/GPU refresh, the 60-second trend, CPU/GPU switching without collector restart, and explicit “GPU activity share” rows. The compiled XCUITest is skipped in the default scheme because this Mac timed out enabling Xcode UI automation mode; the equivalent interaction was completed manually through accessibility inspection.
- [x] 6. Sustained alerts and notification delivery
  - Completed: 2026-07-19 13:06 CST
  - Output: independent resource alert state machines, approved sustained-duration gates, ten-minute red repeat interval, cached notification authorization, two-second CPU/GPU merge window, and transient top-offender notification context.
  - Verification: focused tests first failed on missing alert types, then all three timing scenarios passed; the signed default scheme reports 18/18 passing tests. Denied or errored notification authorization is cached as a normal state and does not stop monitoring.
- [x] 7. Safe process termination and on-demand Root helper
  - Completed: 2026-07-19 13:31 CST
  - Output: PID/start-time identity revalidation, protected-process policy shared by app and helper, same-user TERM with separately gated force KILL, fresh local authentication for every Root operation, fixed NSSecureCoding XPC messages, on-demand legacy blessing/removal adapter, exact-path cleanup, mutual code-signing requirements, and a 15-second idle helper.
  - Verification: focused termination tests first failed on missing types, then 6/6 passed including a real disposable child; the signed default scheme passed 24/24 tests. Xcode embedded the arm64 helper at `Contents/Library/LaunchServices/com.cpualert.helper`, both binaries passed strict and explicit Team requirement checks, both required Mach-O plist sections are present, and both system installation paths remain absent while idle.
- [x] 8. Settings, first run, localization, and accessibility
  - Completed: 2026-07-19 14:04 CST
  - Output: validated persisted thresholds, notification and login-item preferences, explicit helper controls, first-run guidance, four-tab settings, deterministic UI launch states, five/ten-row choices, English and Simplified Chinese String Catalog coverage, text-plus-color pressure semantics, and accessible process/group actions.
  - Verification: settings tests first failed because the store and model did not exist, then 2/2 passed; the signed default scheme passed 26/26 tests. Xcode compiled the String Catalog and UI-test target. Real Computer Use checks covered Chinese normal/critical/GPU-group/first-run/settings states and an English GPU-unavailable Top-5 state; the accessibility tree exposed pressure text, correct GPU activity-share semantics, settings controls, and guarded termination actions. Strict deep signing passed, and no notification, login-item, or helper-install side effect was triggered.
- [ ] 9. Stress fixtures, performance gates, signing, and operational documentation

## Execution notes

- 2026-07-19: Full Xcode is available at `/Applications/Xcode.app`; the global developer directory still points to CommandLineTools, so commands use an explicit `DEVELOPER_DIR`.
- 2026-07-19: Two valid local signing identities are available. The personal Team ID is stored only in ignored `Config/Local.xcconfig`.
- 2026-07-19: Xcode's macOS UI runner cannot enable automation mode in this desktop session. UI test sources remain buildable, while the shared default scheme skips that target and real accessibility-driven checks provide the runtime evidence.
- 2026-07-19: The public macOS 13+ XPC code-signing requirement APIs provide the pre-delegate peer-signature gate; the helper repeats a `SecCode` validity check before exporting its object. No private `NSXPCConnection.auditToken` selector is used.
- 2026-07-19: Deterministic UI launch states use in-memory settings and snapshots, so bilingual accessibility and unavailable-data flows can be inspected without changing the user's notification, login-item, or helper state.
