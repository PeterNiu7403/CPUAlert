<#
.SYNOPSIS
  Capture WinMoe route screenshots for the current Windows display scale and write an environment report.

.DESCRIPTION
  Does NOT change system DPI (that requires admin + logoff). Instead:
  1. Detects the current process / primary monitor DPI via Win32.
  2. Records expected physical sizes from the DpiScaleMatrix (DIP → px).
  3. Launches WinMoe for each main route and captures a window screenshot.
  4. Optionally captures Tray HUD when -IncludeHud is set.
  5. Writes artifacts under artifacts/dpi-matrix/<timestamp>/.

  To complete the full 100/125/150/200 matrix, re-run this script after
  changing Settings → Display → Scale (and signing out if Windows requires it).

.EXAMPLE
  pwsh -File scripts/capture-dpi-matrix.ps1
  pwsh -File scripts/capture-dpi-matrix.ps1 -IncludeHud -NoBuild
#>
param(
    [switch]$NoBuild,
    [switch]$IncludeHud,
    [string[]]$Routes = @("clean", "apps", "optimize", "analyze", "status"),
    [int]$TimeoutSeconds = 45,
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$runLocal = Join-Path $root "run-local.ps1"
if (-not (Test-Path -LiteralPath $runLocal)) {
    throw "run-local.ps1 not found at $runLocal"
}

function Get-PrimaryDpi {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class WinMoeDpiProbe {
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    public const int LOGPIXELSX = 88;
    public static uint PrimaryDpi() {
        var dc = GetDC(IntPtr.Zero);
        try { return (uint)GetDeviceCaps(dc, LOGPIXELSX); }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }
}
"@ -ErrorAction SilentlyContinue

    try {
        $dpi = [WinMoeDpiProbe]::PrimaryDpi()
        if ($dpi -gt 0) { return [uint32]$dpi }
    } catch { }

    return [uint32]96
}

function Get-ScalePercent([uint32]$Dpi) {
    return [int][Math]::Round(($Dpi / 96.0) * 100.0)
}

function Get-ExpectedPhysical([uint32]$Dpi, [int]$DipW, [int]$DipH) {
    $w = [int][Math]::Round($DipW * $Dpi / 96.0, [MidpointRounding]::AwayFromZero)
    $h = [int][Math]::Round($DipH * $Dpi / 96.0, [MidpointRounding]::AwayFromZero)
    return @{ Width = $w; Height = $h }
}

$dpi = Get-PrimaryDpi
$percent = Get-ScalePercent $dpi
$nearest = switch ($true) {
    { $percent -le 112 } { 100; break }
    { $percent -le 137 } { 125; break }
    { $percent -le 175 } { 150; break }
    default { 200 }
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root "artifacts\dpi-matrix\$stamp-dpi$dpi"
} else {
    $OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$mainExpected = Get-ExpectedPhysical $dpi 1194 768
$hudExpected = Get-ExpectedPhysical $dpi 340 720

Write-Host "=== WinMoe DPI matrix capture ==="
Write-Host "Primary DPI: $dpi ($percent%)  nearest matrix: $nearest%"
Write-Host "Expected main physical: $($mainExpected.Width)x$($mainExpected.Height)"
Write-Host "Expected HUD physical:  $($hudExpected.Width)x$($hudExpected.Height)"
Write-Host "Output: $OutputRoot"

$matrixRows = @(
    @{ Label = "100%"; Dpi = 96;  Main = "1194x768";  Hud = "340x720" },
    @{ Label = "125%"; Dpi = 120; Main = "1493x960";  Hud = "425x900" },
    @{ Label = "150%"; Dpi = 144; Main = "1791x1152"; Hud = "510x1080" },
    @{ Label = "200%"; Dpi = 192; Main = "2388x1536"; Hud = "680x1440" }
)

$report = New-Object System.Text.StringBuilder
[void]$report.AppendLine("# WinMoe environment DPI capture")
[void]$report.AppendLine("")
[void]$report.AppendLine("- Timestamp: $(Get-Date -Format o)")
[void]$report.AppendLine("- Machine: $env:COMPUTERNAME")
[void]$report.AppendLine("- OS: $([System.Environment]::OSVersion.VersionString)")
[void]$report.AppendLine("- Primary DPI: **$dpi** ($percent%)")
[void]$report.AppendLine("- Nearest matrix scale: **$nearest%**")
[void]$report.AppendLine("- Expected main window (physical): **$($mainExpected.Width)×$($mainExpected.Height)** from 1194×768 DIP")
[void]$report.AppendLine("- Expected Tray HUD (physical): **$($hudExpected.Width)×$($hudExpected.Height)** from 340×720 DIP")
[void]$report.AppendLine("")
[void]$report.AppendLine("## Full matrix (reference — re-run after changing Display Scale)")
[void]$report.AppendLine("")
[void]$report.AppendLine("| Scale | DPI | Main physical | HUD physical | Captured this run |")
[void]$report.AppendLine("| --- | ---: | ---: | ---: | --- |")
foreach ($row in $matrixRows) {
    $mark = if ([int]$row.Dpi -eq [int]$dpi) { "✓ current" } else { "re-run at this scale" }
    [void]$report.AppendLine("| $($row.Label) | $($row.Dpi) | $($row.Main) | $($row.Hud) | $mark |")
}
[void]$report.AppendLine("")
[void]$report.AppendLine("## Windows capability boundaries")
[void]$report.AppendLine("")
[void]$report.AppendLine("| Id | Status | Notes |")
[void]$report.AppendLine("| --- | --- | --- |")
[void]$report.AppendLine("| fan-rpm | unavailable | UI segments only; no SMC |")
[void]$report.AppendLine("| battery-accessories | unavailable | no public API |")
[void]$report.AppendLine("| battery-main | available | GetSystemPowerStatus |")
[void]$report.AppendLine("| silent-app-updates | unavailable | quiet empty state |")
[void]$report.AppendLine("| startup-inventory | read-only | Run keys + folders |")
[void]$report.AppendLine("| multi-monitor-dpi | supported | HUD uses anchor monitor DPI |")
[void]$report.AppendLine("")
[void]$report.AppendLine("## Screenshots")
[void]$report.AppendLine("")

$first = $true
foreach ($route in $Routes) {
    $safe = $route.ToLowerInvariant()
    $shot = Join-Path $OutputRoot ("route-{0}.png" -f $safe)
    Write-Host "Capturing route=$safe ..."

    $args = @(
        "-NoProfile",
        "-File", $runLocal,
        "-SmokeTest",
        "-NoTray",
        "-Route", $safe,
        "-ScreenshotPath", $shot,
        "-TimeoutSeconds", "$TimeoutSeconds"
    )
    if ($NoBuild -or -not $first) {
        $args += "-NoBuild"
    }
    if ($safe -eq "analyze") {
        $args += "-AnalyzeAutoScan"
    }
    if ($safe -eq "clean") {
        # Keep idle planet surface for visual parity; skip heavy clean autoscan.
    }

    & powershell @args
    if ($LASTEXITCODE -ne 0) {
        throw "capture failed for route=$safe exit=$LASTEXITCODE"
    }

    $exists = Test-Path -LiteralPath $shot
    $sizeNote = ""
    if ($exists) {
        Add-Type -AssemblyName System.Drawing
        $img = [System.Drawing.Image]::FromFile($shot)
        try {
            $sizeNote = "$($img.Width)×$($img.Height) px"
        } finally {
            $img.Dispose()
        }
    }

    [void]$report.AppendLine("- ``route-$safe.png`` — $(if ($exists) { $sizeNote } else { "MISSING" })")
    $first = $false
}

if ($IncludeHud) {
    $hudShot = Join-Path $OutputRoot "tray-hud.png"
    Write-Host "Capturing tray HUD ..."
    $args = @(
        "-NoProfile",
        "-File", $runLocal,
        "-SmokeTest",
        "-ShowTrayHud",
        "-Route", "status",
        "-ScreenshotPath", $hudShot,
        "-TimeoutSeconds", "$TimeoutSeconds",
        "-NoBuild"
    )
    & powershell @args
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "HUD capture failed (exit $LASTEXITCODE); continuing report."
    } else {
        [void]$report.AppendLine("- ``tray-hud.png`` — captured with -ShowTrayHud")
    }
}

[void]$report.AppendLine("")
[void]$report.AppendLine("## Manual checklist")
[void]$report.AppendLine("")
[void]$report.AppendLine("Compare against ``.research/mole-ui/{clean,uninstall,optimize,analyze,status}.jpg`` (gitignored research refs):")
[void]$report.AppendLine("")
[void]$report.AppendLine("- [ ] Capsule 38 DIP height, white selected pill")
[void]$report.AppendLine("- [ ] Clean/Optimize circular planet, no debug log")
[void]$report.AppendLine("- [ ] Status 4×2 + process table")
[void]$report.AppendLine("- [ ] Analyze left rail + treemap earth tones")
[void]$report.AppendLine("- [ ] Apps footer Remove N")
[void]$report.AppendLine("- [ ] No horizontal clip at this scale")
[void]$report.AppendLine("")
[void]$report.AppendLine("To finish the matrix, set Display Scale to each of 100/125/150/200% and re-run:")
[void]$report.AppendLine('```powershell')
[void]$report.AppendLine('powershell -File scripts/capture-dpi-matrix.ps1 -NoBuild')
[void]$report.AppendLine('```')

$reportPath = Join-Path $OutputRoot "REPORT.md"
$report.ToString() | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "Report: $reportPath"
Write-Host "Done."
