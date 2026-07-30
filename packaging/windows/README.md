# Windows Packaging

MoleWindows Windows follows the upstream MoleWindows install rhythm: package manager first, direct download as a fallback.

## Artifacts

Release tooling writes:

- installer: `artifacts\release\MoleWindows-v0.1.0-preview.1-win-x64-setup.exe`
- portable ZIP fallback: `artifacts\release\MoleWindows-v0.1.0-preview.1-win-x64.zip`
- checksum file: `artifacts\release\SHA256SUMS.txt`
- release notes: `artifacts\release\RELEASE_NOTES.md`
- WinGet manifests: `artifacts\release\winget\PeterNiu\MoleWindows\0.1.0-preview.1\`

The installer is built from `MoleWindows.iss` with Inno Setup. It installs per-user to `%LOCALAPPDATA%\Programs\MoleWindows`, creates a Start Menu shortcut named `MoleWindows`, and points that shortcut at the internal `MoleWindows.exe`.

## WinGet

The generated manifest targets:

- PackageIdentifier: `PeterNiu.MoleWindows`
- PackageName: `MoleWindows`
- InstallerType: `inno`
- Scope: `user`
- Architecture: `x64`

Validate locally with:

```powershell
winget validate .\artifacts\release\winget\PeterNiu\MoleWindows\0.1.0-preview.1
```

## Signing

No code signing is performed for `v0.1.0-preview.1`. Users should verify SHA256 and expect Windows SmartScreen reputation prompts for direct downloads. Stricter Application Control policies can block the unsigned setup executable until a signed release is available.
