param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnetCandidates = @(
    (Join-Path $root ".tools\dotnet\dotnet.exe")
)
$dotnet = $dotnetCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $dotnet) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        $dotnet = $dotnetCommand.Source
    }
}

if (-not $dotnet) {
    throw "A .NET SDK was not found. Install the .NET 10 SDK and try again."
}

$project = Join-Path $root "src\StickyNotes\StickyNotes.csproj"
$output = Join-Path $root "artifacts\publish\$Runtime"

& $dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published to: $output"
