#Requires -RunAsAdministrator
# Setup Windows Task Scheduler entry for StrategyAutoTraderMobileAPI auto-start
# Run as Administrator

$ErrorActionPreference = "Stop"

$taskName = "StrategyAutoTraderMobileAPI"
$oldTaskName = "MobileUI.Api"
$taskDescription = "Strategy Auto Trader Mobile UI API sidecar - runs on logon/boot, restarts on failure"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $scriptDir "publish\api"
$exePath = Join-Path $publishDir "MobileUI.Api.exe"

# Remove existing task(s) if present (old name and new name)
Write-Host "Checking for existing task..."
foreach ($name in @($taskName, $oldTaskName)) {
    $existingTask = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "Removing existing task '$name'..."
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
    }
}

# Create action: run published exe directly
$action = New-ScheduledTaskAction `
    -Execute $exePath `
    -WorkingDirectory $publishDir

# Create trigger: at logon
$triggerLogon = New-ScheduledTaskTrigger -AtLogOn

# Create trigger: at boot (system startup)
$triggerBoot = New-ScheduledTaskTrigger -AtStartup

# Create settings: restart on failure
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew

# Register the task to run as SYSTEM (no user interaction needed)
Write-Host "Creating scheduled task..."
Register-ScheduledTask `
    -TaskName $taskName `
    -Description $taskDescription `
    -Action $action `
    -Trigger @($triggerLogon, $triggerBoot) `
    -Settings $settings `
    -RunLevel Highest `
    -User "SYSTEM" | Out-Null

Write-Host "Task '$taskName' registered successfully"
Write-Host "  - Triggers: At logon + At system startup"
Write-Host "  - Restart policy: Up to 3 times, 1 min interval"
Write-Host "  - Working directory: $scriptDir"
Write-Host ""
Write-Host "To verify: Get-ScheduledTask -TaskName '$taskName' | Select-Object *"
Write-Host "To disable: Disable-ScheduledTask -TaskName '$taskName'"
Write-Host "To remove: Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false"
