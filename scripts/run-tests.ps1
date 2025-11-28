<#
.SYNOPSIS
  Run unit and/or integration tests and collect artifacts.

.DESCRIPTION
  Helper script to run the unit tests (fast) and integration E2E tests (Playwright) from the repository root.
  - Creates an `artifacts/` directory if missing
  - Runs unit tests and writes TRX + console log to `artifacts/`
  - Runs integration tests by delegating to `scripts/run-e2e.ps1` so the helper will start the app and collect Playwright artifacts
  - Returns non-zero exit code if any selected test step fails

.PARAMETER Unit
  Run only the unit tests (t12Project.Tests)

.PARAMETER Integration
  Run only the integration (Playwright) tests via `scripts/run-e2e.ps1`.

.PARAMETER All
  Run both Unit and Integration tests (default if none specified)

.PARAMETER NoStart
  When running integration tests, pass through the `-NoStart` flag to `scripts/run-e2e.ps1` so it does not attempt to start the web app.

.EXAMPLE
  .\scripts\run-tests.ps1 -All

#>

param(
    [switch]$Unit,
    [switch]$Integration,
    [switch]$All,
    [switch]$NoStart,
    [string]$ArtifactsDir = ".\artifacts"
)

Set-StrictMode -Version Latest

function Ensure-Directory($p) {
  if (-not (Test-Path -Path $p)) {
    New-Item -ItemType Directory -Path $p | Out-Null
  }
}

# Default to All when no flags provided
if (-not ($Unit -or $Integration -or $All)) {
    $All = $true
}

Ensure-Directory $ArtifactsDir

$script:failed = $false

if ($Unit -or $All) {
    Write-Host "==> Running unit tests..." -ForegroundColor Cyan
    $unitTrx = Join-Path $ArtifactsDir "unit-tests.trx"
    $unitLog = Join-Path $ArtifactsDir "unit-tests.log"

    dotnet test .\t12Project.Tests\t12Project.Tests.csproj --logger "trx;LogFileName=$unitTrx" 2>&1 | Tee-Object -FilePath $unitLog
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Unit tests failed (exit code $LASTEXITCODE)" -ForegroundColor Red
        $script:failed = $true
    } else {
        Write-Host "Unit tests passed" -ForegroundColor Green
    }
}

if ($Integration -or $All) {
    Write-Host "==> Running integration tests (Playwright)..." -ForegroundColor Cyan
    $integrationLog = Join-Path $ArtifactsDir "integration-tests.log"

    # Build the command to call the existing run-e2e helper. The helper handles starting/stopping the app and collects artifacts.
    $e2eScript = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "run-e2e.ps1"
    if (-not (Test-Path -Path $e2eScript)) {
        Write-Host "Could not find $e2eScript. Ensure scripts/run-e2e.ps1 exists." -ForegroundColor Red
        exit 2
    }

  $e2eArgs = @()
  if ($NoStart) { $e2eArgs += '-NoStart' }

  # Execute helper and capture output
  & $e2eScript @e2eArgs 2>&1 | Tee-Object -FilePath $integrationLog
    $e2eExit = $LASTEXITCODE
    if ($e2eExit -ne 0) {
        Write-Host "Integration tests failed (exit code $e2eExit)" -ForegroundColor Red
        $script:failed = $true
    } else {
        Write-Host "Integration tests passed" -ForegroundColor Green
    }
}

if ($script:failed) {
    Write-Host "One or more test suites failed. See $ArtifactsDir for logs and TRX files." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "All selected test suites passed." -ForegroundColor Green
    exit 0
}
