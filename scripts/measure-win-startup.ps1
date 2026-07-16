[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,

    [ValidateRange(1, 50)]
    [int]$Iterations = 7,

    [ValidateRange(1, 120)]
    [int]$TimeoutSeconds = 20,

    [ValidateRange(0, 10000)]
    [int]$CooldownMilliseconds = 800,

    [switch]$StopExistingProcess
)

$ErrorActionPreference = "Stop"

$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$knownProcessNames = @("DeckDeckDeck", "DeckDeckDeck.App")
$startupLogPath = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) `
    "NumpadPromptLauncher\logs\app.log"

$windowNativeSource = @'
using System;
using System.Runtime.InteropServices;

public static class StartupWindowNative
{
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

Add-Type -TypeDefinition $windowNativeSource -Language CSharp

function Stop-DeckDeckDeckProcesses {
    $processes = Get-Process -Name $knownProcessNames -ErrorAction SilentlyContinue
    if (-not $processes) {
        return
    }

    if (-not $StopExistingProcess) {
        $ids = ($processes.Id | Sort-Object) -join ", "
        throw "DeckDeckDeck is already running (PID: $ids). Close it first or pass -StopExistingProcess."
    }

    $processes | Stop-Process -Force
    $processes | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

function Get-StartupTimingLineCount {
    if (-not (Test-Path -LiteralPath $startupLogPath)) {
        return 0
    }

    return @(
        Select-String -LiteralPath $startupLogPath -Pattern "Startup timing" -SimpleMatch
    ).Count
}

function Get-LatestStartupTimingLine {
    if (-not (Test-Path -LiteralPath $startupLogPath)) {
        return $null
    }

    return Select-String `
        -LiteralPath $startupLogPath `
        -Pattern "Startup timing" `
        -SimpleMatch |
        Select-Object -Last 1 -ExpandProperty Line
}

function Get-TimingMarkerMilliseconds {
    param(
        [string]$Line,
        [string]$Marker
    )

    if ($Line -match ("{0}(?:=\d+ms)?@(\d+)ms" -f [regex]::Escape($Marker))) {
        return [int]$Matches[1]
    }

    return $null
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    $ordered = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($ordered.Count * $Percentile) - 1)
    return $ordered[$index]
}

Stop-DeckDeckDeckProcesses

$samples = @()
for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
    $timingLineCountBefore = Get-StartupTimingLineCount
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $executablePath -PassThru
    $firstVisibleMilliseconds = $null
    $startupTimingLine = $null

    try {
        while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            $process.Refresh()
            if ($process.HasExited) {
                throw "The measured process exited before its window became visible."
            }

            if ($null -eq $firstVisibleMilliseconds `
                -and $process.MainWindowHandle -ne [IntPtr]::Zero `
                -and [StartupWindowNative]::IsWindowVisible($process.MainWindowHandle)) {
                $firstVisibleMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
            }

            if ((Get-StartupTimingLineCount) -gt $timingLineCountBefore) {
                $startupTimingLine = Get-LatestStartupTimingLine
                break
            }

            Start-Sleep -Milliseconds 10
        }

        if ($null -eq $firstVisibleMilliseconds) {
            throw "No visible DeckDeckDeck window was found within $TimeoutSeconds seconds."
        }

        $samples += [pscustomobject]@{
            Run = $iteration
            FirstVisibleExternalMs = [Math]::Round($firstVisibleMilliseconds, 1)
            FirstContentInternalMs = if ($startupTimingLine) {
                Get-TimingMarkerMilliseconds $startupTimingLine "first content rendered"
            } else {
                $null
            }
            StartupReadyInternalMs = if ($startupTimingLine) {
                Get-TimingMarkerMilliseconds $startupTimingLine "startup ready"
            } else {
                $null
            }
        }
    }
    finally {
        if (-not $process.HasExited) {
            $process | Stop-Process -Force
            $process | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
        }
    }

    if ($CooldownMilliseconds -gt 0 -and $iteration -lt $Iterations) {
        Start-Sleep -Milliseconds $CooldownMilliseconds
    }
}

$samples | Format-Table -AutoSize

$summary = foreach ($property in @(
    "FirstVisibleExternalMs",
    "FirstContentInternalMs",
    "StartupReadyInternalMs"
)) {
    $values = @($samples.$property | Where-Object { $null -ne $_ })
    if ($values.Count -eq 0) {
        continue
    }

    [pscustomobject]@{
        Metric = $property
        Samples = $values.Count
        MedianMs = Get-Percentile $values 0.5
        P90Ms = Get-Percentile $values 0.9
        MaxMs = ($values | Measure-Object -Maximum).Maximum
    }
}

$summary | Format-Table -AutoSize
