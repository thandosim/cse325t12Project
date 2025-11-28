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
    [int]$TimeoutSeconds = 60
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
            return $true
        }
        Start-Sleep -Seconds 1
    }
    return $false
}

Write-Host "Starting app: dotnet run --project $AppProject"
$appProc = Start-Process -FilePath 'dotnet' -ArgumentList 'run','--project',$AppProject -PassThru -NoNewWindow

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
    $env:E2E_BASEURL = $BaseUrl

    Write-Host "Running tests: dotnet test $TestProject"
    $testExit = & dotnet test $TestProject
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Host "Tests failed with exit code $exitCode"
    }
    else {
        Write-Host "Tests passed"
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

    # restore env var
    if ($null -ne $old) {
        $env:E2E_BASEURL = $old
    }
    else {
        Remove-Item Env:E2E_BASEURL -ErrorAction SilentlyContinue
    }
}
