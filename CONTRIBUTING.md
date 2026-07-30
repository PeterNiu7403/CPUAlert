# Contributing to WinMoe

Thanks for helping make WinMoe usable on Windows. This branch should stay easy to review, safe to run, and honest about WinMoe limitations.

## Development Rules

- Keep UI, ViewModel, and service boundaries clear. UI code should handle WinUI events; ViewModels should hold state and commands; services should own Windows/Mole integration.
- Prefer WinMoe when it exposes a safe, non-interactive command. Use native Windows fallback only when Mole lacks JSON output or background-safe behavior.
- Keep destructive operations preview-first and confirmation-gated.
- Preserve local-only agent access: HTTP must bind to loopback and MCP destructive actions must remain opt-in.
- Use English for code names, comments, scripts, workflow text, and docs.

## Pull Request Checklist

- `dotnet build .\WinMoe.csproj -p:Platform=x64 -nr:false -v:minimal`
- `dotnet build .\Tests\WinMoe.Tests\WinMoe.Tests.csproj -nr:false -v:minimal`
- `dotnet test .\Tests\WinMoe.Tests\WinMoe.Tests.csproj --no-build -v:minimal`
- If UI or startup behavior changed, run at least one `run-local.ps1` smoke route. Add `-ScreenshotPath artifacts\ui-smoke\<route>.png` when the change affects layout, navigation, charts, or autoscan result surfaces.
- Update `docs/winmoe/design.md` and immediately update
  `docs/winmoe/tasks.md` when changing engine seams, release gates,
  or known gaps.

## Branch Readiness

A change is release-ready only when tests pass, the app can open a visible WinUI window, `/health` responds when HTTP is enabled, and release documentation remains accurate for a new contributor.
