<#
.SYNOPSIS
  Create a diagnostic bundle for the first failed test in a TRX: extracts failing test details, surrounding server logs, and related Playwright artifacts into a single ZIP.

.PARAMETER ArtifactsDir
  The artifacts directory (default: ./artifacts)

.PARAMETER TrxFile
  Optional path to the TRX file. If omitted, script will look for artifacts/e2e-test-results.trx

.PARAMETER LogFile
  Optional path to the console log file (default: artifacts/test-output.log)

.PARAMETER OutBundle
  Optional path for the output ZIP file. Default: artifacts/triage-YYYYMMDD-HHMMSS.zip
#>
param(
    [string]$ArtifactsDir = (Join-Path (Split-Path -Path $MyInvocation.MyCommand.Path -Parent) '..\artifacts' | Resolve-Path -ErrorAction SilentlyContinue | ForEach-Object { $_.ProviderPath }),
    [string]$TrxFile = '',
    [string]$LogFile = '',
    [string]$OutBundle = ''
)

if (-not (Test-Path $ArtifactsDir)) { Write-Error "Artifacts directory not found: $ArtifactsDir"; exit 2 }

if ([string]::IsNullOrWhiteSpace($TrxFile)) { $TrxFile = Join-Path $ArtifactsDir 'e2e-test-results.trx' }
if ([string]::IsNullOrWhiteSpace($LogFile)) { $LogFile = Join-Path $ArtifactsDir 'test-output.log' }
if (-not (Test-Path $TrxFile)) { Write-Error "TRX file not found: $TrxFile"; exit 2 }
if (-not (Test-Path $LogFile)) { Write-Warning "Log file not found: $LogFile" }

[xml]$trx = Get-Content -Path $TrxFile

# Find first failed unit test result
$failed = $trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -and $_.outcome -eq 'Failed' } | Select-Object -First 1
if (-not $failed) {
    Write-Host "No failed tests found in TRX. Nothing to triage."; exit 0
}

$testName = $failed.testName
$failureMessage = ''
$failureStack = ''
try { $failureMessage = $failed.Output.ErrorInfo.Message } catch {}
try { $failureStack = $failed.Output.ErrorInfo.StackTrace } catch {}

# Extract start/end times if available
$startTime = $null
$endTime = $null
try { $startTime = [datetime]$failed.startTime } catch {}
try { $endTime = [datetime]$failed.endTime } catch {}

Write-Host "Failed test: $testName"
Write-Host "Start: $startTime  End: $endTime"

# Prepare bundle directory
$bundleDir = Join-Path $ArtifactsDir ("triage-" + (Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $bundleDir -Force | Out-Null

# Write summary file
$summary = @()
$summary += "Test: $testName"
$summary += "TRX: $TrxFile"
$summary += "Start: $startTime"
$summary += "End: $endTime"
$summary += ""
$summary += "Failure message:" 
$summary += $failureMessage
$summary += ""
$summary += "Stack trace:" 
$summary += $failureStack
$summary += ""
$summaryPath = Join-Path $bundleDir 'summary.txt'
$summary | Out-File -FilePath $summaryPath -Encoding utf8

# Gather server log context: try to extract lines within +/- 30 seconds of test times
$logBundlePath = Join-Path $bundleDir 'server-log-snippet.txt'
if (Test-Path $LogFile -and $startTime -and $endTime) {
    # try to detect ISO timestamp at start of line
    $lines = Get-Content -Path $LogFile -ErrorAction SilentlyContinue
    $matched = @()
    $windowStart = $startTime.AddSeconds(-30)
    $windowEnd = $endTime.AddSeconds(30)

    foreach ($line in $lines) {
        # try to parse timestamp from the line (common formats)
        if ($line -match '^\s*(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)') {
            $ts = $matches['ts']
            try {
                $ldt = [datetime]::Parse($ts)
                if ($ldt -ge $windowStart -and $ldt -le $windowEnd) { $matched += $line }
            } catch {}
        }
        elseif ($line -match '^\s*(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})') {
            $ts = $matches['ts']
            try {
                $ldt = [datetime]::ParseExact($ts, 'yyyy-MM-dd HH:mm:ss', $null)
                if ($ldt -ge $windowStart -and $ldt -le $windowEnd) { $matched += $line }
            } catch {}
        }
    }

    if ($matched.Count -gt 0) {
        $matched | Out-File -FilePath $logBundlePath -Encoding utf8
    }
    else {
        # fallback: include last 300 lines
        Get-Content -Path $LogFile -Tail 300 | Out-File -FilePath $logBundlePath -Encoding utf8
    }
}
else {
    if (Test-Path $LogFile) {
        Get-Content -Path $LogFile -Tail 300 | Out-File -FilePath $logBundlePath -Encoding utf8
    }
}

# Copy Playwright artifacts that look related (trace zips, screenshots, page html) - take recent ones
$playwrightDir = Join-Path $ArtifactsDir 'playwright'
if (Test-Path $playwrightDir) {
    # pick files modified within +/- 2 minutes of endTime if available, otherwise last 50 files
    if ($endTime) {
        $candidates = Get-ChildItem -Path $playwrightDir -Recurse -File | Where-Object { ($_.LastWriteTime -ge $endTime.AddMinutes(-2)) -and ($_.LastWriteTime -le $endTime.AddMinutes(2)) }
    }
    else {
        $candidates = Get-ChildItem -Path $playwrightDir -Recurse -File | Sort-Object LastWriteTime -Descending | Select-Object -First 50
    }

    foreach ($f in $candidates) {
        $dest = Join-Path $bundleDir $f.Name
        Copy-Item -Path $f.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
    }
}

# Copy TRX and HTML report if present
Copy-Item -Path $TrxFile -Destination $bundleDir -ErrorAction SilentlyContinue
$reportHtml = Join-Path $ArtifactsDir 'e2e-report.html'
if (Test-Path $reportHtml) { Copy-Item -Path $reportHtml -Destination $bundleDir -ErrorAction SilentlyContinue }

# Create ZIP bundle
if ([string]::IsNullOrWhiteSpace($OutBundle)) { $OutBundle = Join-Path $ArtifactsDir ("triage-$(Get-Date -Format yyyyMMdd-HHmmss).zip") }
if (Test-Path $OutBundle) { Remove-Item -Path $OutBundle -Force -ErrorAction SilentlyContinue }
Compress-Archive -Path (Join-Path $bundleDir '*') -DestinationPath $OutBundle -Force

Write-Host "Created triage bundle: $OutBundle"
Write-Host "Bundle contents (preview):"
Get-ChildItem -Path $bundleDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

# Done
exit 0
