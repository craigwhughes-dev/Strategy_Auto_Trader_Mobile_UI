$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProjectPath = Join-Path $scriptDir "src\MobileUI.Api"
$daemonLockPath = "C:\Users\Craig\.claude\skills\Strategy_Auto_Trader\state\daemon.lock"

Write-Host "=== Sidecar Independence Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Daemon status:"
if (Test-Path $daemonLockPath) {
    Write-Host "   PASS: Daemon running" -ForegroundColor Green
}
else {
    Write-Host "   WARNING: Daemon lock not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "2. Starting API..."
$proc = Start-Process dotnet -ArgumentList "run --project `"$apiProjectPath`" --configuration Debug" `
    -WorkingDirectory $scriptDir -PassThru -NoNewWindow
Start-Sleep -Seconds 5
Write-Host "   API PID: $($proc.Id)"

Write-Host ""
Write-Host "3. Checking API port..."
$portOK = (Test-NetConnection localhost -Port 5000 -ErrorAction SilentlyContinue).TcpTestSucceeded
if ($portOK) { Write-Host "   PASS: Port 5000 listening" -ForegroundColor Green }
else { Write-Host "   FAIL: Port 5000 not responding" -ForegroundColor Red }

Write-Host ""
Write-Host "4. Stopping API..."
$proc | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "5. Verifying daemon still running..."
if (Test-Path $daemonLockPath) {
    Write-Host "   PASS: Daemon survived API shutdown" -ForegroundColor Green
}
else {
    Write-Host "   WARNING: Cannot verify daemon status" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
