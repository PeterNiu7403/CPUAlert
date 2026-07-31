param(
    [switch]$NoBuild,
    [switch]$SmokeTest,
    [switch]$ShowTrayHud,
    [switch]$ShowCleanScreen,
    [switch]$NoTray,
    [switch]$Restart,
    [switch]$RequireHealth,
    [switch]$KeepRunning,
    [string]$Route,
    [string]$AnalyzeRoot,
    [switch]$AnalyzeAutoScan,
    [string]$InstallerRoot,
    [switch]$InstallerAutoScan,
    [switch]$CleanAutoScan,
    [switch]$OptimizeAutoScan,
    [string]$ScreenshotPath,
    [int]$TimeoutSeconds = 30,
    [int]$SettleSeconds = 0,
    [string]$WindowTitle = "WinMoe"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "WinMoe.csproj"
$exe = Join-Path $root "bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\WinMoe.exe"
$startupLog = Join-Path $env:LOCALAPPDATA "WinMoe\startup.log"

function Stop-ExistingWinMoe {
    param([string]$ExpectedPath)

    Get-Process -Name "WinMoe" -ErrorAction SilentlyContinue | ForEach-Object {
        $path = $null
        try {
            $path = $_.Path
        } catch {
            $path = $null
        }

        if ([string]::IsNullOrWhiteSpace($path) -or
            [string]::Equals($path, $ExpectedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $_.Id -Force
        }
    }
}

function Ensure-SmokeWin32 {
    if ("WinMoeSmokeWin32" -as [type]) {
        return
    }

    Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class WinMoeSmokeWin32 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // GetWindowRect is DPI-virtualized when the caller is not DPI-aware, which
    // under-reports the window size on scaled displays. The DWM extended frame
    // bounds are always physical pixels — required for a correct screenshot.
    public static RECT GetPhysicalWindowRect(IntPtr hWnd) {
        RECT rect;
        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT))) == 0) {
            return rect;
        }
        if (GetWindowRect(hWnd, out rect)) {
            return rect;
        }
        return rect;
    }
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@
}

function Get-WinMoeWindow {
    param(
        [int]$ProcessId,
        [string]$Title = "WinMoe"
    )

    Write-Host "Get-WinMoeWindow: pid=$ProcessId title='$Title' len=$($Title.Length)"
    Ensure-SmokeWin32
    # Scriptblocks turned into .NET delegates do not see function-locals;
    # a script-scoped copy is required for the callback to read the wanted title.
    $script:SmokeWantedTitle = $Title
    $windows = New-Object System.Collections.Generic.List[object]
    [WinMoeSmokeWin32]::EnumWindows({
        param($hWnd, $lParam)

        if (-not [WinMoeSmokeWin32]::IsWindowVisible($hWnd)) {
            return $true
        }

        $windowProcessId = [uint32]0
        [void][WinMoeSmokeWin32]::GetWindowThreadProcessId($hWnd, [ref]$windowProcessId)
        if ($windowProcessId -ne [uint32]$ProcessId) {
            return $true
        }

        $titleBuilder = New-Object System.Text.StringBuilder 256
        [void][WinMoeSmokeWin32]::GetWindowText($hWnd, $titleBuilder, $titleBuilder.Capacity)
        $title = $titleBuilder.ToString()
        if ($title -eq $script:SmokeWantedTitle) {
            $windows.Add([pscustomobject]@{ Handle = $hWnd; Title = $title; ProcessId = $windowProcessId }) | Out-Null
        }

        return $true
    }, [IntPtr]::Zero) | Out-Null

    if ($windows.Count -eq 0) {
        return $null
    }

    return $windows[0]
}

function Save-WinMoeWindowScreenshot {
    param(
        [object]$Window,
        [string]$Path
    )

    Ensure-SmokeWin32
    Add-Type -AssemblyName System.Drawing

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        $outputPath = [System.IO.Path]::GetFullPath($Path)
    } else {
        $outputPath = [System.IO.Path]::GetFullPath((Join-Path $root $Path))
    }

    $outputDirectory = Split-Path -Parent $outputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }

    $rect = [WinMoeSmokeWin32]::GetPhysicalWindowRect($Window.Handle)
    Write-Host "Smoke window: handle=$($Window.Handle) title='$($Window.Title)' rect=$($rect.Left),$($rect.Top) $($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top)"

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "WinMoe window bounds were invalid for screenshot capture."
    }

    $hwndTopMost = [IntPtr]::new(-1)
    $hwndNoTopMost = [IntPtr]::new(-2)
    $swRestore = 9
    $swpNoSize = [uint32]0x0001
    $swpNoMove = [uint32]0x0002
    $swpShowWindow = [uint32]0x0040
    $topMostFlags = [uint32]($swpNoSize -bor $swpNoMove -bor $swpShowWindow)

    [void][WinMoeSmokeWin32]::ShowWindow($Window.Handle, $swRestore)
    [void][WinMoeSmokeWin32]::SetWindowPos($Window.Handle, $hwndTopMost, 0, 0, 0, 0, $topMostFlags)
    [void][WinMoeSmokeWin32]::SetForegroundWindow($Window.Handle)
    Start-Sleep -Milliseconds 700

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))
        } finally {
            $graphics.Dispose()
        }

        $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
        [void][WinMoeSmokeWin32]::SetWindowPos($Window.Handle, $hwndNoTopMost, 0, 0, 0, 0, $topMostFlags)
    }

    Write-Host "Screenshot: $outputPath"
}

function Read-StartupLogTail {
    if (Test-Path -LiteralPath $startupLog) {
        Get-Content -LiteralPath $startupLog -Tail 30 | Out-String
    } else {
        "<startup log not found at $startupLog>"
    }
}

function Read-StartupLogSinceOffset {
    param([long]$Offset)

    if (-not (Test-Path -LiteralPath $startupLog)) {
        return ""
    }

    $stream = $null
    try {
        $stream = [System.IO.File]::Open($startupLog, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        if ($Offset -gt 0 -and $Offset -lt $stream.Length) {
            $stream.Seek($Offset, [System.IO.SeekOrigin]::Begin) | Out-Null
        }

        $reader = [System.IO.StreamReader]::new($stream)
        $stream = $null
        try {
            return $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }
    } finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Test-WinMoeAssemblyLaunchAllowed {
    param([string]$AssemblyPath)

    if (-not (Test-Path -LiteralPath $AssemblyPath)) {
        throw "WinMoe.dll was not found next to the app executable. Run without -NoBuild first."
    }

    try {
        [void][System.Reflection.AssemblyName]::GetAssemblyName($AssemblyPath)
    } catch {
        $baseException = $_.Exception.GetBaseException()
        $message = $baseException.Message
        $hresult = "0x{0:X8}" -f [System.BitConverter]::ToUInt32([System.BitConverter]::GetBytes([int]$baseException.HResult), 0)

        if ($message -match "Application Control policy|blocked this file" -or $hresult -eq "0x800711C7") {
            $signatureStatus = "Unknown"
            try {
                $signatureStatus = (Get-AuthenticodeSignature -FilePath $AssemblyPath).Status
            } catch {
                $signatureStatus = "Unavailable"
            }

            throw @"
Windows Application Control blocked WinMoe before app startup.

Blocked assembly: $AssemblyPath
HRESULT: $hresult
Signature: $signatureStatus
Reason: $message

This is an OS policy block, not a WinMoe UI hang. The current build must be allowed by WDAC/AppLocker/Smart App Control or signed with a certificate trusted by that policy.
"@
        }

        throw "WinMoe assembly preflight failed for '$AssemblyPath': $message"
    }
}

if (-not $NoBuild) {
    dotnet build $project -p:Platform=x64 -nr:false -v:minimal
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "WinMoe.exe was not found. Run without -NoBuild first."
}

$assembly = [System.IO.Path]::ChangeExtension($exe, ".dll")
Test-WinMoeAssemblyLaunchAllowed -AssemblyPath $assembly

if ($Restart) {
    Stop-ExistingWinMoe -ExpectedPath $exe
}

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $exe
$startInfo.WorkingDirectory = Split-Path -Parent $exe
$startInfo.UseShellExecute = $false

if ($ShowTrayHud) {
    $startInfo.Environment["WINMOE_SHOW_TRAY_HUD"] = "1"
}

if ($ShowCleanScreen) {
    $startInfo.Environment["WINMOE_SHOW_CLEAN_SCREEN"] = "1"
}

if ($NoTray) {
    $startInfo.Environment["WINMOE_DISABLE_TRAY"] = "1"
}

if (-not [string]::IsNullOrWhiteSpace($Route)) {
    $startInfo.Environment["WINMOE_START_ROUTE"] = $Route
}

if (-not [string]::IsNullOrWhiteSpace($AnalyzeRoot)) {
    $startInfo.Environment["WINMOE_ANALYZE_ROOT"] = $AnalyzeRoot
}

if ($AnalyzeAutoScan) {
    $startInfo.Environment["WINMOE_ANALYZE_AUTOSCAN"] = "1"
}

if (-not [string]::IsNullOrWhiteSpace($InstallerRoot)) {
    $startInfo.Environment["WINMOE_INSTALLER_ROOT"] = $InstallerRoot
}

if ($InstallerAutoScan) {
    $startInfo.Environment["WINMOE_INSTALLER_AUTOSCAN"] = "1"
}

if ($CleanAutoScan) {
    $startInfo.Environment["WINMOE_CLEAN_AUTOSCAN"] = "1"
}

if ($OptimizeAutoScan) {
    $startInfo.Environment["WINMOE_OPTIMIZE_AUTOSCAN"] = "1"
}

$startupLogOffset = 0
if (Test-Path -LiteralPath $startupLog) {
    $startupLogOffset = (Get-Item -LiteralPath $startupLog).Length
}

$process = [System.Diagnostics.Process]::Start($startInfo)
Write-Host "Started WinMoe PID $($process.Id)"
Write-Host "Startup log: $startupLog"

if (-not $SmokeTest) {
    return
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$window = $null
$health = $null
$routeOpened = [string]::IsNullOrWhiteSpace($Route)
$autoScanFinished = $true
$autoScanPattern = $null

if ($CleanAutoScan) {
    $autoScanFinished = $false
    $autoScanPattern = "[clean] Clean autoscan finished:"
}

if ($OptimizeAutoScan) {
    $autoScanFinished = $false
    $autoScanPattern = "[optimize] Optimize auto-preview finished:"
}

if ($InstallerAutoScan) {
    $autoScanFinished = $false
    $autoScanPattern = "[installer] Installer autoscan finished:"
}

if ($AnalyzeAutoScan) {
    $autoScanFinished = $false
    $autoScanPattern = "[analyze] Analyze autoscan finished:"
}

try {
    # HUD title is "WinMoe " + U+72B6 U+6001 (状态); clean-screen title is
    # "WinMoe " + U+64E6 U+5C4F (擦屏); built from char codes so this
    # file stays ASCII-only (Windows PowerShell misreads UTF-8-no-BOM scripts).
    $windowTitle = $WindowTitle
    if ($ShowTrayHud) { $windowTitle = "WinMoe " + [char]0x72B6 + [char]0x6001 }
    if ($ShowCleanScreen) { $windowTitle = "WinMoe " + [char]0x64E6 + [char]0x5C4F }
    Write-Host "ShowTrayHud=$ShowTrayHud windowTitle='$windowTitle'"
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            throw "WinMoe exited early with code $($process.ExitCode)."
        }

        if ($null -eq $window) {
            $window = Get-WinMoeWindow -ProcessId $process.Id -Title $windowTitle
        }

        if ($null -eq $health) {
            try {
                $health = Invoke-RestMethod -Uri "http://127.0.0.1:9277/health" -TimeoutSec 2
            } catch {
                $health = $null
            }
        }

        $startupLogNew = Read-StartupLogSinceOffset -Offset $startupLogOffset
        if ($startupLogNew -match "\[xaml_unhandled\]") {
            throw "WinMoe recorded a XAML unhandled exception during smoke startup."
        }

        if (-not $routeOpened -and $startupLogNew.Contains("[navigation] Opening startup route: $Route")) {
            $routeOpened = $true
        }

        if (-not $autoScanFinished -and $null -ne $autoScanPattern -and $startupLogNew.Contains($autoScanPattern)) {
            $autoScanFinished = $true
        }

        if ($null -ne $window -and ($null -ne $health -or -not $RequireHealth) -and $routeOpened -and $autoScanFinished) {
            break
        }

        Start-Sleep -Milliseconds 500
    }

    if ($null -eq $window) {
        throw "WinMoe main window was not found within $TimeoutSeconds seconds."
    }

    if ($RequireHealth -and $null -eq $health) {
        throw "WinMoe HTTP health did not respond within $TimeoutSeconds seconds."
    }

    if (-not $routeOpened) {
        throw "WinMoe startup route '$Route' was not opened within $TimeoutSeconds seconds."
    }

    if (-not $autoScanFinished) {
        throw "WinMoe autoscan did not finish within $TimeoutSeconds seconds."
    }

    Write-Host "GUI smoke passed: main window is visible."
    if ($null -ne $health) {
        Write-Host "Health: ok=$($health.ok), port=$($health.port), latest_sample_at=$($health.latest_sample_at)"
    } else {
        Write-Host "Health: not available or disabled."
    }

    if ($SettleSeconds -gt 0) {
        Write-Host "Settling for $SettleSeconds second(s) before capture ..."
        Start-Sleep -Seconds $SettleSeconds
    }

    Save-WinMoeWindowScreenshot -Window $window -Path $ScreenshotPath
} catch {
    Write-Host "Startup log tail:"
    Write-Host (Read-StartupLogTail)
    throw
} finally {
    if (-not $KeepRunning -and $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
