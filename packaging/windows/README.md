# Windows Packaging

WinMoe uses a package-manager-first release flow, with direct download as a fallback.

## Artifacts

Release tooling writes:

- installer: `artifacts\release\WinMoe-v0.1.0-preview.1-win-x64-setup.exe`
- portable ZIP fallback: `artifacts\release\WinMoe-v0.1.0-preview.1-win-x64.zip`
- checksum file: `artifacts\release\SHA256SUMS.txt`
- release notes: `artifacts\release\RELEASE_NOTES.md`
- WinGet manifests: `artifacts\release\winget\PeterNiu\WinMoe\0.1.0-preview.1\`

The installer is built from `WinMoe.iss` with Inno Setup. It installs per-user to `%LOCALAPPDATA%\Programs\WinMoe`, creates a Start Menu shortcut named `WinMoe`, and points that shortcut at the internal `WinMoe.exe`.

`scripts\build-release.ps1` requires the final HTTPS repository URL. It
deliberately blocks public release while that URL still points to `CPUAlert`;
after the GitHub repository is renamed, pass the canonical URL with
`-RepositoryUrl`.

## WinGet

The generated manifest targets:

- PackageIdentifier: `PeterNiu.WinMoe`
- PackageName: `WinMoe`
- InstallerType: `inno`
- Scope: `user`
- Architecture: `x64`

Validate locally with:

```powershell
winget validate .\artifacts\release\winget\PeterNiu\WinMoe\0.1.0-preview.1
```

## Signing

No code signing is performed for `v0.1.0-preview.1`. Users should verify SHA256 and expect Windows SmartScreen reputation prompts for direct downloads. Stricter Application Control policies can block the unsigned setup executable until a signed release is available.
