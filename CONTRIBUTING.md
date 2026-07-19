# Contributing to CPUAlert

Thank you for helping improve CPUAlert. Contributions should preserve the project's privacy, safety, and metric-honesty guarantees.

## Before you start

- Search existing issues before opening a new one.
- Use a focused issue for behavior changes that affect permissions, process termination, private GPU APIs, or the privileged helper.
- Do not include process names, usernames, signing identities, Team IDs, benchmark traces, crash reports, or other machine-specific data unless it has been redacted.
- Security-sensitive reports belong in GitHub private vulnerability reporting; see [SECURITY.md](SECURITY.md).

## Development setup

CPUAlert requires an Apple silicon Mac, macOS 15 or later, and full Xcode. Create an ignored `Config/Local.xcconfig` containing your own development Team ID:

```xcconfig
CPU_ALERT_DEVELOPMENT_TEAM = YOUR_TEAM_ID
```

Build and run the unit tests:

```bash
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer

xcodebuild build \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -configuration Debug \
  -destination 'platform=macOS,arch=arm64' \
  -derivedDataPath build/DerivedData

xcodebuild test \
  -project CPUAlert.xcodeproj \
  -scheme CPUAlert \
  -destination 'platform=macOS,arch=arm64' \
  -derivedDataPath build/DerivedData \
  -only-testing:CPUAlertTests
```

UI tests use deterministic launch states. Run them when changing navigation, labels, localization, accessibility, group disclosure, settings, or menu behavior.

## Project expectations

- Keep CPU monitoring usable when GPU monitoring fails.
- Describe estimated GPU values as estimates; do not present coalition attribution as a direct per-process hardware counter.
- Keep rankings bounded and avoid retaining process history.
- Do not introduce networking, telemetry, analytics, or upload behavior without explicit design discussion and prominent documentation.
- Keep privileged operations fixed and narrowly scoped. Never add arbitrary command, path, executable, or environment execution to the helper.
- Authenticate every root termination and preserve PID plus process-start-time validation.
- Add or update tests for behavior changes.
- Keep English and Simplified Chinese user-facing strings aligned.

## Pull requests

Create a focused branch, use clear commits, and explain:

- what changed and why;
- user-visible or security impact;
- metric-semantics impact, if any;
- tests and manual checks performed;
- screenshots for visible UI changes.

By contributing, you agree that your contribution is licensed under the repository's `GPL-3.0-or-later` license.
