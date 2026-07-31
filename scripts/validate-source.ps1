$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

$xmlFiles = @(
    (Join-Path $root "App.xaml"),
    (Join-Path $root "MainWindow.xaml"),
    (Join-Path $root "Package.appxmanifest")
) + @(
    Get-ChildItem -Path (Join-Path $root "Pages"), (Join-Path $root "Views"), (Join-Path $root "Styles") `
        -Recurse -File -Include *.xaml |
        Select-Object -ExpandProperty FullName
)

foreach ($path in $xmlFiles) {
    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.Load($path)
}

$requiredAssets = @(
    "Assets\AppIcon.ico",
    "Assets\StoreLogo.png",
    "Assets\SplashScreen.scale-200.png",
    "Assets\LockScreenLogo.scale-200.png",
    "Assets\Wide310x150Logo.scale-200.png",
    "Assets\Square150x150Logo.scale-200.png",
    "Assets\Square44x44Logo.scale-200.png",
    "Assets\Square44x44Logo.targetsize-24_altform-unplated.png",
    "Assets\Square44x44Logo.targetsize-48_altform-lightunplated.png",
    "Assets\Brand\winmoe-mark.svg",
    "Assets\Hero\clean.png",
    "Assets\Hero\software.png",
    "Assets\Hero\optimize.png",
    "Assets\Hero\analyze.png",
    "Assets\Hero\status.png"
)

foreach ($relativePath in $requiredAssets) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required asset is missing: $relativePath"
    }

    if ((Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Required asset is empty: $relativePath"
    }
}

$legacyPaths = @(
    "CPUAlert.xcodeproj",
    "CPUAlertApp",
    "CPUAlertHelper",
    "CPUAlertTests",
    "CPUAlertUITests"
)

foreach ($relativePath in $legacyPaths) {
    if (Test-Path -LiteralPath (Join-Path $root $relativePath)) {
        throw "Legacy macOS project path is still present: $relativePath"
    }
}

$project = Get-Content -LiteralPath (Join-Path $root "WinMoe.csproj") -Raw
if ($project -notmatch 'Assets\\Hero\\\*\.png') {
    throw "WinMoe.csproj does not package the page hero assets."
}

$cleanupPage = Get-Content -LiteralPath (Join-Path $root "Pages\CleanupPage.xaml") -Raw
$optimizePage = Get-Content -LiteralPath (Join-Path $root "Pages\OptimizePage.xaml") -Raw
$cleanupViewModel = Get-Content -LiteralPath (Join-Path $root "ViewModels\CleanupViewModel.cs") -Raw
# The clean apply flow is review-gated and must always pass through the
# operation-plan validator + recycle-bin service (audited execution contract).
if ($cleanupPage -notmatch 'IsEnabled="\{Binding CanClean\}"') {
    throw "Clean apply button must stay gated by the review-state CanClean binding."
}

if ($cleanupViewModel -notmatch '_planValidator\.ValidateForApply' -or
    $cleanupViewModel -notmatch '_safeDeletionService\.DeleteFileOrDirectory') {
    throw "Clean apply must route through OperationPlanValidator and the recycle-bin deletion service."
}

Write-Host "Validated $($xmlFiles.Count) XML/XAML files, $($requiredAssets.Count) assets, legacy removal, and P0 safety gates."
