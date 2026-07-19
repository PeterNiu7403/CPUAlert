# CPUAlert Implementation Progress

> This file is the execution source of truth. Update it immediately after each verified task.

## Progress

| Metric | Value |
|---|---:|
| Total tasks | 9 |
| Completed | 1 (11.1%) |
| Remaining | 8 (88.9%) |
| Last updated | 2026-07-19 12:11 CST |

## Tasks

- [x] 1. Native project and menu bar rendering gate
  - Completed: 2026-07-19 12:11 CST
  - Output: native six-target Xcode project, shared build configuration, agent app plist, fixed-width `MenuBarLabel`, signed Debug app, and evidence-driven `NSStatusItem` fallback.
  - Verification: `plutil` passed; all six targets are listed; arm64 macOS Debug build succeeded under Swift 6; strict `codesign` verification passed; real-screen capture showed both CPU and GPU rows after the fallback.
- [ ] 2. Metric domain and adaptive sampling policy
- [ ] 3. Whole-machine, process, and thread CPU collectors
- [ ] 4. Fail-closed GPU utilization and coalition attribution
- [ ] 5. Unified sampling engine and monitor panel
- [ ] 6. Sustained alerts and notification delivery
- [ ] 7. Safe process termination and on-demand Root helper
- [ ] 8. Settings, first run, localization, and accessibility
- [ ] 9. Stress fixtures, performance gates, signing, and operational documentation

## Execution notes

- 2026-07-19: Full Xcode is available at `/Applications/Xcode.app`; the global developer directory still points to CommandLineTools, so commands use an explicit `DEVELOPER_DIR`.
- 2026-07-19: Two valid local signing identities are available. The personal Team ID is stored only in ignored `Config/Local.xcconfig`.
