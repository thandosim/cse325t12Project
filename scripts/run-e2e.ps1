<#
.SYNOPSIS
  Start the web app, wait until it responds, run Playwright E2E tests, then stop the app.

.DESCRIPTION
  This script is a convenience wrapper for local E2E runs on Windows/PowerShell. It:
   - starts the web app (dotnet run) from a project
   - waits for the app to be reachable on a candidate URL (or the provided -BaseUrl)
   - sets the E2E_BASEURL env var for the test run
   - runs `dotnet test` against the Playwright test project
   - stops the web app and returns the test exit code

.PARAMETER AppProject
  Path to the web app project to run (relative to repo root). Default: .\t12Project.csproj

.PARAMETER TestProject
  Path to the Playwright test project csproj. Default: .\t12Project.Playwright\t12Project.Playwright.csproj

.PARAMETER BaseUrl
  Optional: explicitly provide the full URL the tests should target (e.g. https://localhost:7218). If omitted the script will probe common local ports.

.PARAMETER TimeoutSeconds
  How many seconds to wait for the app to become reachable (default 60).

EXAMPLE
  # run against default projects and auto-detect URL
  .\scripts\run-e2e.ps1

  # provide explicit base url
  .\scripts\run-e2e.ps1 -BaseUrl 'https://localhost:7218'
#>

param(
    [string]$AppProject = '.\t12Project.csproj',
    [string]$TestProject = '.\t12Project.Playwright\t12Project.Playwright.csproj',
    [string]$BaseUrl = '',
    [int]$TimeoutSeconds = 60,
    [string]$DotnetPath = 'dotnet',
    [switch]$InstallBrowsers,
    [switch]$NoStart = $false
)

function Wait-ForTcpPort {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMs = 1000
    )

    try {
        $client = New-Object System.Net.Sockets.TcpClient
    $iar = $client.BeginConnect($HostName, $Port, $null, $null)
        $wait = $iar.AsyncWaitHandle.WaitOne($TimeoutMs)
        if (-not $wait) {
            $client.Close()
            return $false
        }
        $client.EndConnect($iar)
        $client.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Wait-ForUrlUp {
    param(
        [string]$Url,
        [int]$TotalTimeoutSeconds = 60
    )

    try {
        $u = [uri]$Url
    }
    catch {
        return $false
    }

    $deadline = (Get-Date).AddSeconds($TotalTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Wait-ForTcpPort -HostName $u.Host -Port $u.Port -TimeoutMs 1000) {
            try {
                $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -SkipCertificateCheck -Method Head -TimeoutSec 5
                if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                    return $true
                }
            }
            catch {
                # ignore and retry
            }
        }
        Start-Sleep -Seconds 1
    }
    return $false
}

# prepare artifacts directory
$scriptRoot = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$artifactsDir = Join-Path $scriptRoot '..\artifacts' | Resolve-Path -ErrorAction SilentlyContinue | ForEach-Object { $_.ProviderPath }
if (-not $artifactsDir) {
    $artifactsDir = Join-Path $scriptRoot '..\artifacts'
    New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
}

if (-not $NoStart) {
    Write-Host "Starting app: $DotnetPath run --project $AppProject"
    $appProc = Start-Process -FilePath $DotnetPath -ArgumentList 'run','--project',$AppProject -PassThru -NoNewWindow
}
else {
    Write-Host "-NoStart specified; not starting app. Assuming server is already running."
    $appProc = $null
}

try {
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        $candidates = @(
            'https://localhost:7218',
            'http://localhost:5004',
            'http://localhost:5000',
            'https://localhost:5001'
        )
        $found = $null
        foreach ($c in $candidates) {
            Write-Host "Probing $c ..."
            if (Wait-ForUrlUp -Url $c -TotalTimeoutSeconds $TimeoutSeconds) {
                $found = $c
                break
            }
        }
        if ($null -eq $found) {
            Write-Warning "Failed to auto-detect a listening URL after $TimeoutSeconds seconds. Provide -BaseUrl to override."
            throw "App did not appear to start in time."
        }
        $BaseUrl = $found
    }

    Write-Host "Using E2E_BASEURL=$BaseUrl"
    $old = $env:E2E_BASEURL
    $oldArtifactsEnv = $env:E2E_ARTIFACTS_DIR
    $env:E2E_BASEURL = $BaseUrl

    # prepare Playwright artifacts directory and expose it to tests via E2E_ARTIFACTS_DIR
    $playwrightArtifactsDir = Join-Path $artifactsDir 'playwright'
    if (-not (Test-Path $playwrightArtifactsDir)) { New-Item -ItemType Directory -Path $playwrightArtifactsDir -Force | Out-Null }
    $env:E2E_ARTIFACTS_DIR = $playwrightArtifactsDir

    if ($InstallBrowsers) {
        Write-Host "Installing Playwright browsers..."
        Push-Location (Split-Path $TestProject)
        & $DotnetPath tool restore
        & $DotnetPath tool run playwright install
        Pop-Location
    }

    Write-Host "Running tests: $DotnetPath test $TestProject"
    $trxPath = Join-Path $artifactsDir 'e2e-test-results.trx'
    $logPath = Join-Path $artifactsDir 'test-output.log'
    $dotnetArgs = @('test', $TestProject, '--logger', "trx;LogFileName=$trxPath")

    & $DotnetPath @dotnetArgs 2>&1 | Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Host "Tests failed with exit code $exitCode"
        Write-Host "Test logs and results saved to: $artifactsDir"

        # Attempt to collect common Playwright artifacts if present
        $foundArtifacts = @()
        $searchNames = @('playwright-report','playwright-report.html','TestResults','test-results')
        foreach ($name in $searchNames) {
            $matches = Get-ChildItem -Path $scriptRoot -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq $name }
            foreach ($m in $matches) {
                $dest = Join-Path $artifactsDir ($m.Name + '-' + (Get-Random))
                Copy-Item -Path $m.FullName -Destination $dest -Recurse -Force -ErrorAction SilentlyContinue
                $foundArtifacts += $dest
            }
        }
        if ($foundArtifacts.Count -gt 0) {
            Write-Host "Collected additional artifacts:"
            $foundArtifacts | ForEach-Object { Write-Host " - $_" }
        }

        # Run triage script to create a diagnostic bundle (if present)
        $triageScript = Join-Path $scriptRoot 'triage-artifacts.ps1'
        if (Test-Path $triageScript) {
            Write-Host "Creating triage bundle using $triageScript ..."
            try {
                & pwsh -NoProfile -ExecutionPolicy Bypass -File $triageScript -ArtifactsDir $artifactsDir
            }
            catch {
                Write-Warning "Failed to run triage script: $_"
            }
        }
    }
    else {
        Write-Host "Tests passed"
        Write-Host "Test logs and results saved to: $artifactsDir"
    }

    # Attempt to convert TRX -> HTML for easier browsing (trx2html)
    try {
        if (Test-Path $trxPath) {
            Write-Host "Converting TRX to HTML report..."
            if (-not (Get-Command trx2html -ErrorAction SilentlyContinue)) {
                Write-Host "trx2html not found. Installing dotnet tool trx2html (one-time)..."
                dotnet tool install --global trx2html | Out-Null
            }
            $htmlOut = Join-Path $artifactsDir 'e2e-report.html'
            trx2html $trxPath -o $htmlOut 2>$null
            if (Test-Path $htmlOut) { Write-Host "TRX converted to HTML: $htmlOut" }
            else { Write-Warning "Failed to produce HTML from TRX." }
        }
    }
    catch {
        Write-Warning "TRX -> HTML conversion failed: $_"
    }

    # Video retention: keep recent N videos and/or remove files older than retention days
    try {
        $videoRetentionDays = 14
        $maxKeep = 50
        $videoFiles = Get-ChildItem -Path $playwrightArtifactsDir -Include *.webm -Recurse -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
        if ($videoFiles) {
            # delete by age
            $old = $videoFiles | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$videoRetentionDays) }
            foreach ($f in $old) { Remove-Item -Path $f.FullName -ErrorAction SilentlyContinue }

            # delete oldest if over maxKeep
            $videoFiles = Get-ChildItem -Path $playwrightArtifactsDir -Include *.webm -Recurse -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
            if ($videoFiles.Count -gt $maxKeep) {
                $toRemove = $videoFiles[$maxKeep..($videoFiles.Count - 1)]
                foreach ($f in $toRemove) { Remove-Item -Path $f.FullName -ErrorAction SilentlyContinue }
            }
        }
    }
    catch {
        Write-Warning "Video retention cleanup failed: $_"
    }

    # return the same code
    exit $exitCode
}
finally {
    Write-Host "Stopping app (if still running)"
    try {
        if ($appProc -and -not $appProc.HasExited) {
            Stop-Process -Id $appProc.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
        }
    }
    catch {
        Write-Warning "Failed to stop the app process: $_"
    }

    # restore env vars
    if ($null -ne $old) {
        $env:E2E_BASEURL = $old
    }
    else {
        Remove-Item Env:E2E_BASEURL -ErrorAction SilentlyContinue
    }

    if ($null -ne $oldArtifactsEnv) {
        $env:E2E_ARTIFACTS_DIR = $oldArtifactsEnv
    }
    else {
        Remove-Item Env:E2E_ARTIFACTS_DIR -ErrorAction SilentlyContinue
    }
}
