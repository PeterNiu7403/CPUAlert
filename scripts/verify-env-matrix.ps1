<#
.SYNOPSIS
  Offline environment matrix verification (no UI required).

.DESCRIPTION
  Validates DPI DIP→physical expectations and emits platform capability notes.
  Safe for CI: pure math + documentation dump.

.EXAMPLE
  pwsh -File scripts/verify-env-matrix.ps1
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$matrix = @(
    @{ Percent = 100; Dpi = 96;  MainW = 1194; MainH = 768;  HudW = 340; HudH = 720 },
    @{ Percent = 125; Dpi = 120; MainW = 1493; MainH = 960;  HudW = 425; HudH = 900 },
    @{ Percent = 150; Dpi = 144; MainW = 1791; MainH = 1152; HudW = 510; HudH = 1080 },
    @{ Percent = 200; Dpi = 192; MainW = 2388; MainH = 1536; HudW = 680; HudH = 1440 }
)

function Get-Physical([int]$Dip, [int]$Dpi) {
    return [int][Math]::Round($Dip * $Dpi / 96.0, [MidpointRounding]::AwayFromZero)
}

$failed = 0
Write-Host "DPI layout matrix verification"
Write-Host ("{0,-8} {1,5} {2,14} {3,14}" -f "Scale", "DPI", "Main", "HUD")
foreach ($row in $matrix) {
    $mw = Get-Physical 1194 $row.Dpi
    $mh = Get-Physical 768 $row.Dpi
    $hw = Get-Physical 340 $row.Dpi
    $hh = Get-Physical 720 $row.Dpi
    $ok = ($mw -eq $row.MainW -and $mh -eq $row.MainH -and $hw -eq $row.HudW -and $hh -eq $row.HudH)
    $status = if ($ok) { "OK" } else { "FAIL" }
    if (-not $ok) { $failed++ }
    Write-Host ("{0,-8} {1,5} {2,14} {3,14}  {4}" -f ("{0}%" -f $row.Percent), $row.Dpi, ("{0}x{1}" -f $mw, $mh), ("{0}x{1}" -f $hw, $hh), $status)
}

if ($failed -gt 0) {
    throw "DPI matrix verification failed ($failed rows)."
}

Write-Host ""
Write-Host "Windows platform boundaries (honest):"
Write-Host "  fan-rpm              unavailable (UI only)"
Write-Host "  battery-accessories  unavailable"
Write-Host "  battery-main         available"
Write-Host "  silent-app-updates   unavailable (quiet empty)"
Write-Host "  startup-inventory    read-only"
Write-Host "  multi-monitor-dpi    HUD anchor-monitor DPI"

$outDir = Join-Path $root "artifacts\env-matrix"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $outDir "verify-$stamp.md"
@"
# Environment matrix verify

- When: $(Get-Date -Format o)
- Result: PASS ($($matrix.Count) DPI rows)

## DPI

| Scale | DPI | Main | HUD |
| --- | ---: | ---: | ---: |
| 100% | 96 | 1194×768 | 340×720 |
| 125% | 120 | 1493×960 | 425×900 |
| 150% | 144 | 1791×1152 | 510×1080 |
| 200% | 192 | 2388×1536 | 680×1440 |

## Next for full visual matrix

1. Set Display Scale to 100%, run ``scripts/capture-dpi-matrix.ps1 -NoBuild``
2. Repeat for 125 / 150 / 200%
3. Check ``artifacts/dpi-matrix/*/REPORT.md`` checklists
"@ | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Report: $reportPath"
Write-Host "PASS"
