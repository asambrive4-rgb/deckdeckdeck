param(
    [switch]$SelfContained,
    [switch]$SingleFile
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$Project = Join-Path $Root "src\DeckDeckDeck.App\DeckDeckDeck.App.csproj"
$Output = Join-Path $Root "artifacts\publish\win-x64"

$sdks = & dotnet --list-sdks 2>$null
if (-not $sdks) {
    Write-Host ".NET SDK was not found."
    Write-Host "Install the .NET SDK that matches the project target framework, then run this script again."
    Write-Host "Project target framework: net10.0-windows"
    exit 1
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$singleFileValue = if ($SingleFile) { "true" } else { "false" }

if (Test-Path $Output) {
    Remove-Item $Output -Recurse -Force
}

$publishArgs = @(
    "publish",
    $Project,
    "-c",
    "Release",
    "-r",
    "win-x64",
    "--self-contained",
    $selfContainedValue,
    "-o",
    $Output,
    "-p:PublishSingleFile=$singleFileValue"
)

if ($SingleFile) {
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $Output "DeckDeckDeck.App.exe"
Write-Host ""
Write-Host "Done."
Write-Host "EXE: $exePath"
