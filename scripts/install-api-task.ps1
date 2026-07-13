<#
.SYNOPSIS
Registers a Windows Scheduled Task to auto-start the MobileUI.Api sidecar at user logon.

.DESCRIPTION
This script publishes the ASP.NET Core API and registers it with Task Scheduler to run at logon
with automatic restart on failure. The API requires the following configuration before first run:

REQUIRED CONFIGURATION:
  1. Certificate thumbprint (CERTIFICATE_THUMBPRINT environment variable or appsettings.json):
     - Self-signed certificate installed in Current User\My certificate store
     - Thumbprint: Check with: certutil -store My | findstr /I "Thumbprint"
     - Default: 7618F28C90EE396840E9B980773F8A69147E86CC (dev/test only)

  2. API key (STRATEGY_API_KEY environment variable):
     - Secret string used in X-Api-Key header for POST/PUT/DELETE requests
     - Must be set before running the task
     - GET requests (/api/positions, /api/health, /api/trades/recent) do not require the key
     - See src/MobileUI.Api/Middleware/ApiKeyAuthenticationMiddleware.cs

  3. Daemon state paths (configured in appsettings.json or environment):
     - DaemonState:AppStatusPath: C:\path\to\Strategy_Auto_Trader\state\app_status.json
     - DaemonState:JournalPath: C:\path\to\Strategy_Auto_Trader\data\journals\live.csv
     - DaemonState:CommandsPath: C:\path\to\Strategy_Auto_Trader\state\commands

  4. Tailscale IP (optional, for remote access):
     - TAILSCALE_INTERFACE_IP: e.g. 100.x.x.x
     - Only needed if binding to a specific Tailscale interface instead of 0.0.0.0

.PARAMETER RepoRoot
The root directory of the Strategy_Auto_Trader_Mobile_UI repository.
Defaults to the parent of the script's directory.

.EXAMPLE
.\install-api-task.ps1 -RepoRoot "C:\Users\Craig\.claude\skills\Strategy_Auto_Trader_Mobile_UI"

#>

param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$taskName = "MobileUI.Api"
$publishDir = Join-Path $RepoRoot "publish\api"
$apiExe = Join-Path $publishDir "MobileUI.Api.exe"
$apiProject = Join-Path $RepoRoot "src\MobileUI.Api\MobileUI.Api.csproj"

Write-Host "Installing Windows Scheduled Task: $taskName"
Write-Host "Repo root: $RepoRoot"
Write-Host "Publish directory: $publishDir"

# Unregister existing task if present
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Unregistering existing task..."
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false | Out-Null
}

# Publish the API in Release mode
Write-Host "Publishing API to $publishDir..."
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}

$publishArgs = @(
    "publish"
    $apiProject
    "-c", "Release"
    "-o", $publishDir
    "--no-self-contained"
)

& dotnet $publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $apiExe)) {
    throw "Published executable not found: $apiExe"
}

# Create scheduled task
Write-Host "Registering scheduled task..."

$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -RunLevel Limited -LogonType Interactive
$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew

$action = New-ScheduledTaskAction `
    -Execute $apiExe `
    -WorkingDirectory $publishDir

$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

Register-ScheduledTask `
    -TaskName $taskName `
    -Principal $principal `
    -Settings $settings `
    -Action $action `
    -Trigger $trigger `
    -Force | Out-Null

Write-Host "Success! Task '$taskName' registered."
Write-Host ""
Write-Host "Task details:"
Write-Host "  Executable: $apiExe"
Write-Host "  Working directory: $publishDir"
Write-Host "  Trigger: At user logon"
Write-Host "  Restart on failure: Yes (3 retries, 1 minute interval)"
Write-Host ""
Write-Host "IMPORTANT: Before the task runs, ensure:"
Write-Host "  1. CERTIFICATE_THUMBPRINT environment variable is set (or update appsettings.json)"
Write-Host "  2. STRATEGY_API_KEY environment variable is set"
Write-Host "  3. DaemonState paths in appsettings.json point to valid Strategy_Auto_Trader directories"
Write-Host ""
Write-Host "The task will start automatically at next user logon."
Write-Host "To start it now, run: Start-ScheduledTask -TaskName '$taskName'"
