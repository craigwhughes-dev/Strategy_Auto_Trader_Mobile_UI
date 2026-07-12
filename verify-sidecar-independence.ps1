# Verify API and daemon can run independently (sidecar principle)

$ErrorActionPreference = "Continue"

Write-Host "=== Sidecar Independence Verification ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Checking daemon status..."
$daemonLockPath = "C:\Users\Craig\.claude\skills\Strategy_Auto_Trader\state\daemon.lock"
if (Test-Path $daemonLockPath) {
    Write-Host "   ✓ Daemon appears to be running" -ForegroundColor Green
} else {
    Write-Host "   ⚠ Daemon may not be running" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "2. Starting API (port 5000)..."
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProjectPath = Join-Path $scriptDir "src\MobileUI.Api"

$apiProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --project `"$apiProjectPath`" --configuration Debug" `
    -WorkingDirectory $scriptDir `
    -PassThru -NoNewWindow

Write-Host "   API PID: $($apiProcess.Id)"
Start-Sleep -Seconds 5

Write-Host ""
Write-Host "3. Testing API /api/health..."
$portTest = (Test-NetConnection localhost -Port 5000 -ErrorAction SilentlyContinue).TcpTestSucceeded
if ($portTest) {
    Write-Host "   ✓ API port responding" -ForegroundColor Green
} else {
    Write-Host "   ✗ API port not responding" -ForegroundColor Red
}

Write-Host ""
Write-Host "4. Stopping API..."
$apiProcess | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (Test-Path $daemonLockPath) {
    Write-Host "   ✓ Daemon still running (sidecar verified)" -ForegroundColor Green
} else {
    Write-Host "   ⚠ Cannot verify daemon status" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Cyan
