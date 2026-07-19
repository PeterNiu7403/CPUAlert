# CPUAlert Requirements

The authoritative implementation requirements are the global constraints and final acceptance checklist in [`../superpowers/plans/2026-07-19-cpualert-implementation.md`](../superpowers/plans/2026-07-19-cpualert-implementation.md).

## User stories

1. As a Mac user, I want glanceable whole-machine CPU and GPU pressure in the menu bar so I can identify sustained load without opening Activity Monitor.
2. As a Mac user, I want likely CPU processes and GPU application groups ranked honestly so I can investigate a culprit without false per-process GPU precision.
3. As a Mac user, I want rate-limited sustained-load notifications so transient spikes do not become noise.
4. As a Mac user, I want guarded process termination with identity revalidation so I can recover from runaway work without killing a reused or protected PID.

## Acceptance requirements

- RQ-1: WHEN the app runs on Apple Silicon macOS 15 or later THEN it SHALL render fixed-width CPU and GPU rows in a menu-bar extra without a standalone Dock presence.
- RQ-2: WHEN samples arrive THEN CPU values SHALL be normalized to whole-machine `0...100%`, and unavailable GPU data SHALL display as unavailable without stopping CPU monitoring.
- RQ-3: WHEN the panel or pressure state changes THEN sampling SHALL follow the adaptive cadence defined by the implementation plan.
- RQ-4: WHEN a threshold remains crossed for its dwell time THEN alerts SHALL apply hysteresis, one-shot yellow/orange delivery, and a ten-minute red cooldown.
- RQ-5: WHEN termination is requested THEN the app SHALL protect critical processes, bind the request to an exact process identity, use `SIGTERM` first, and require a second confirmation before `SIGKILL`.
- RQ-6: WHEN Root termination is requested THEN the app SHALL authenticate locally and cross only the fixed, mutually authenticated XPC helper boundary.
- RQ-7: WHEN optional/private GPU adapters fail THEN the app SHALL fail closed, stop GPU alerts, and clearly label coalition attribution as an estimate.
- RQ-8: WHEN shipped locally THEN visible copy SHALL support English and Simplified Chinese, and the Release app SHALL be signed and pass strict code-sign verification.

## Non-functional constraints

- No third-party runtime dependencies, telemetry, networking, history persistence, or extra system-monitoring scope.
- Closed-panel green-state target: at most 0.3% CPU, 40 MB resident memory, and one wakeup per second over five minutes in Release.
- The privileged helper remains on demand and exposes no arbitrary command execution.
