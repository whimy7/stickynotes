param(
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publishedExe = Join-Path $root "artifacts\publish\win-x64\StickyNotes.exe"

if (-not $SkipPublish) {
    & (Join-Path $root "publish.ps1")
}

if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published executable not found: $publishedExe"
}

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\StickyNotes"
$installedExe = Join-Path $installDirectory "StickyNotes.exe"
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -LiteralPath $publishedExe -Destination $installedExe -Force

$shell = New-Object -ComObject WScript.Shell
$shortcutLocations = @(
    [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory),
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs")
)
$shortcutName = (-join ([char[]](0x684C, 0x9762, 0x4FBF, 0x7B7E))) + ".lnk"
$shortcutDescription = -join ([char[]](0x684C, 0x9762, 0x4FBF, 0x7B7E))

foreach ($location in $shortcutLocations) {
    $shortcutPath = Join-Path $location $shortcutName
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Description = $shortcutDescription
    $shortcut.Save()
}

Write-Host "Installation complete. Desktop and Start menu shortcuts were created."
