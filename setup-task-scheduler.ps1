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

# Stop the task first so its exe isn't locked when we publish over it
$runningTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($runningTask -and $runningTask.State -eq "Running") {
    Write-Host "Stopping running task before publish..."
    Stop-ScheduledTask -TaskName $taskName
    Start-Sleep -Seconds 2
}

Write-Host "Publishing latest build..."
dotnet publish (Join-Path $scriptDir "src\MobileUI.Api") -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# The task runs as SYSTEM, which has its own empty CurrentUser cert store.
# Copy the HTTPS cert (with private key) into LocalMachine\My so SYSTEM can find it.
$certThumbprint = "5DEC269C2F5F64CE28D86D0089C42CF472B37192"
$existingInLocalMachine = Get-Item "Cert:\LocalMachine\My\$certThumbprint" -ErrorAction SilentlyContinue
if (-not $existingInLocalMachine) {
    Write-Host "Copying certificate into LocalMachine store for SYSTEM access..."
    $sourceCert = Get-Item "Cert:\CurrentUser\My\$certThumbprint" -ErrorAction SilentlyContinue
    if (-not $sourceCert) {
        throw "Certificate $certThumbprint not found in CurrentUser store either. Run generate-certificate.ps1 first."
    }
    $tempPwd = ConvertTo-SecureString -String ([Guid]::NewGuid().ToString()) -Force -AsPlainText
    $pfxPath = Join-Path $env:TEMP "mobileui-cert-transfer.pfx"
    Export-PfxCertificate -Cert $sourceCert -FilePath $pfxPath -Password $tempPwd | Out-Null
    Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\LocalMachine\My -Password $tempPwd | Out-Null
    Remove-Item $pfxPath -Force
    Write-Host "Certificate copied to LocalMachine store."
} else {
    Write-Host "Certificate already present in LocalMachine store."
}

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

Write-Host "Starting task..."
Start-ScheduledTask -TaskName $taskName
Start-Sleep -Seconds 2
$finalState = (Get-ScheduledTask -TaskName $taskName).State
Write-Host "Task '$taskName' registered and state is: $finalState"
Write-Host "  - Triggers: At logon + At system startup"
Write-Host "  - Restart policy: Up to 3 times, 1 min interval"
Write-Host "  - Working directory: $scriptDir"
Write-Host ""
Write-Host "To verify: Get-ScheduledTask -TaskName '$taskName' | Select-Object *"
Write-Host "To disable: Disable-ScheduledTask -TaskName '$taskName'"
Write-Host "To remove: Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false"
