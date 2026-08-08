param(
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"
$running = Get-Process -Name "StickyNotes" -ErrorAction SilentlyContinue
if ($running) {
    throw "Close Sticky Notes before running the uninstall script."
}

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\StickyNotes"
$programsDirectory = Join-Path $env:LOCALAPPDATA "Programs"
$resolvedProgramsDirectory = [System.IO.Path]::GetFullPath($programsDirectory)
$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($installDirectory)

if (-not $resolvedInstallDirectory.StartsWith($resolvedProgramsDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove a path outside the expected install directory."
}

$shortcutName = (-join ([char[]](0x684C, 0x9762, 0x4FBF, 0x7B7E))) + ".lnk"
$shortcutPaths = @(
    (Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)) $shortcutName),
    (Join-Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs") $shortcutName)
)

foreach ($shortcutPath in $shortcutPaths) {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
}

if (Test-Path -LiteralPath $resolvedInstallDirectory) {
    Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
}

if ($RemoveData) {
    $dataDirectory = Join-Path $env:LOCALAPPDATA "StickyNotes"
    if (Test-Path -LiteralPath $dataDirectory) {
        Remove-Item -LiteralPath $dataDirectory -Recurse -Force
    }
}

Write-Host "Uninstall complete."
