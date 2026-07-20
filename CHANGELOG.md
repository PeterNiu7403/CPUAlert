# Changelog

All notable user-facing changes to CPUAlert are documented here.

## [0.2.1] - 2026-07-20

### Added

- Added whole-machine memory pressure monitoring based on active, wired, and compressed pages.
- Added physical-memory process rankings using `ri_phys_footprint` without an additional process scan.
- Added an explicit memory-release workflow that lists safe current-user applications, starts with no selection, asks for confirmation, and requests graceful termination only.
- Added a fixed-width C/G/M menu-bar triptych, three-resource summary cards, and CPU/GPU/memory trend lines.
- Added real-popover UI coverage for resource switching, whole-card GPU group expansion, memory-cleanup interaction, and outside-click dismissal.

### Changed

- GPU process groups now expand when any part of the group card is clicked.
- Settings now expose launch-at-login, notification, display-row, privilege, and first-run controls instead of placeholders.
- The lower-right action is now the concise **Quit** button.
- CPU and memory rankings share one bounded process scan, while application metadata is cached on the main actor to keep interaction responsive.

### Fixed

- Fixed popover controls, sheets, and menus becoming unclickable because a local mouse monitor treated auxiliary windows as outside clicks.
- Preserved automatic dismissal when the user clicks the desktop or another application.
- Prevented unavailable GPU data from disabling CPU or memory monitoring and alerts.

### Validation

- 37 unit tests and 4 UI tests pass.
- Release static analysis and the arm64 archive complete successfully.
- The signed `0.2.1 (Build 6)` app passes deep code-signature verification.
- Closed-panel regression sample: 0.0109% average CPU, 21.093 MB average physical footprint, and 0 package-idle wakeups/s over 10.03 seconds.

## [0.1.2] - 2026-07-19

- Prepared the initial public source release with CPU/GPU monitoring, notifications, settings/onboarding, safe process termination, tests, documentation, and GPL-3.0-or-later licensing.
