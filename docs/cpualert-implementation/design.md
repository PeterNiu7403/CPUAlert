# CPUAlert Design

The full file map, interfaces, algorithms, and verification commands live in [`../superpowers/plans/2026-07-19-cpualert-implementation.md`](../superpowers/plans/2026-07-19-cpualert-implementation.md).

## Architecture summary

- A SwiftUI `MenuBarExtra(.window)` hosts a compact two-resource label and panel.
- A single Swift actor owns sampling cadence and publishes immutable snapshots to a main-actor Observation model.
- Public Mach/libproc collectors produce whole-machine, process, and on-demand thread CPU deltas.
- A fail-closed Darwin bridge dynamically loads IOReport and coalition symbols; global GPU is reported separately from application-group attribution, whose activity shares are scaled to estimated whole-machine contributions for display.
- A pure alert state machine handles dwell time, hysteresis, and cooldown before `UserNotifications` delivery.
- Same-user termination remains in the GUI process. Root termination uses an on-demand, signed legacy `SMJobBless` helper with a fixed secure-coding XPC protocol, caller validation, local authentication, and repeated PID identity checks.
- Settings use validated local persistence, `SMAppService.mainApp`, in-panel onboarding, and a String Catalog.
- Deterministic UI launch arguments inject fixed snapshots and an in-memory settings store, keeping acceptance checks isolated from user preferences and privileged state.

## Key decisions

1. Use `MenuBarExtra` unless the Task 1 rendering gate proves the colored fixed-height label is clipped or stripped.
2. Treat precise per-process GPU utilization as unavailable; show global utilization and scale coalition activity shares by that global value to produce clearly labelled whole-machine contribution estimates.
3. Keep collectors and policies behind narrow protocols so deterministic tests do not require live system load.
4. Keep the local signing selection in ignored `Config/Local.xcconfig`; mutual app/helper requirements pin the certificate leaf's exact Team identifier because `SMJobBless` requires it in both embedded plists.
5. Use the public macOS XPC peer code-signing requirement APIs for the audit-token-bound pre-delegate gate, then repeat `SecCode` validity checking before assigning the helper's exported object. Do not rely on a private `NSXPCConnection.auditToken` selector.
6. Attach Time Profiler to an explicitly launched Release PID with a minimal environment. Measure CPU time, task-owned physical footprint, raw RSS, and package-idle wakeups through `proc_pid_rusage`; reject traces containing sensitive environment-variable keys.

## Test strategy

- Swift Testing covers pure sampling, collector delta math, GPU aggregation, alert timing, settings validation, and termination policy.
- XCTest UI flows use deterministic injected launch state.
- Bounded CPU and Metal fixtures exercise live collectors without targeting unrelated processes.
- Release gates include full tests, static analysis, benchmark modes, archive validation, signature verification, and local launch.
