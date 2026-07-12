# Phase 1 Testing Guide

## Backend API Testing ✅

### Prerequisites
- API project: `src/MobileUI.Api`
- Strategy_Auto_Trader daemon must be running
- Paths in `appsettings.json` must point to Strategy_Auto_Trader state/journal files

### Run API
```bash
cd src/MobileUI.Api
dotnet run
```

API will listen on:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001` (self-signed cert)

### Test Endpoints

**Health Status**
```bash
curl http://localhost:5000/api/health
```
Expected: JSON with `daemonRunning`, `heartbeatAgeSeconds`, `dryRun`, `haltNewEntries`, `markets`

**Open Positions**
```bash
curl http://localhost:5000/api/positions
```
Expected: JSON array of positions with `ticker`, `market`, `currency`, `quantity`, `fillPrice`, `currentPrice`, `unrealizedPnl`

**Recent Trades**
```bash
curl http://localhost:5000/api/trades/recent?count=5
```
Expected: JSON array of closed trades with `ticker`, `dateOpened`, `dateClosed`, `entryPrice`, `exitPrice`, `roundtripPnl`

---

## MAUI App Testing (Manual)

### Build & Run on Windows

```bash
cd src/MobileUI.Maui
dotnet run --framework net10.0-windows10.0.19041.0
```

### Build & Run on Android Emulator

```bash
cd src/MobileUI.Maui
dotnet run --framework net10.0-android
```

### UI Test Checklist

**Status Banner** (top of screen)
- [ ] Daemon status shows "Online" (green) when daemon is running
- [ ] Daemon status shows "Offline" (red) when daemon is not running
- [ ] Paper trading flag (🔄) appears when `dry_run: true`
- [ ] Halted flag (⛔) appears when `halt_new_entries: true`

**Open Positions Section**
- [ ] Positions load from API after page appears
- [ ] Each position shows: Ticker, Quantity, Entry Price, Current Price, Currency
- [ ] P&L is color-coded: Green for positive, Red for negative, Black for zero
- [ ] P&L value displays with proper formatting (2 decimals, +/- signs)

**Recent Trades Section**
- [ ] Shows up to 5 most recent closed trades
- [ ] Each trade displays: Ticker, Date Closed, Roundtrip P&L
- [ ] P&L color coding works (green/red)

**Pull-to-Refresh**
- [ ] Pulling down on the page triggers refresh
- [ ] Loading indicator appears during refresh
- [ ] Status updates to "Updated" when refresh completes
- [ ] All sections (positions, trades, health) refresh together

**Error Handling**
- [ ] If API is offline, shows "Error: ..." message
- [ ] Page doesn't crash if API returns empty data
- [ ] Page doesn't crash if network is unavailable

### Settings Test

Default API URL: `http://192.168.1.100:5000`

To test with local API:
1. Edit `src/MobileUI.Maui/Services/ApiClient.cs` line 19:
   ```csharp
   private string _baseUrl = "http://localhost:5000";
   ```
2. Rebuild and run

---

## Sidecar Independence Verification ✅

```bash
# From repo root:
.\verify-sidecar.ps1
```

Verifies:
- Daemon continues running after API is stopped
- API can start/stop without affecting daemon
- Both processes can coexist

---

## Task Scheduler Setup (Optional)

For automatic API startup on Windows boot/logon:

```bash
# Run as Administrator
.\setup-task-scheduler.ps1
```

This creates a scheduled task named `MobileUI.Api` that:
- Runs at logon and system startup
- Restarts up to 3 times if it crashes (1 min interval)
- Runs as SYSTEM (no user interaction needed)

To disable: `Disable-ScheduledTask -TaskName MobileUI.Api`
To remove: `Unregister-ScheduledTask -TaskName MobileUI.Api -Confirm:$false`

---

## Build & Test Commands

### API Only
```bash
dotnet build src/MobileUI.Api -c Debug
dotnet test tests/MobileUI.Api.Tests -c Debug
```

### MAUI Windows
```bash
dotnet build src/MobileUI.Maui -f net10.0-windows10.0.19041.0 -c Debug
dotnet run --project src/MobileUI.Maui --framework net10.0-windows10.0.19041.0
```

### MAUI Android
```bash
dotnet build src/MobileUI.Maui -f net10.0-android -c Debug
dotnet run --project src/MobileUI.Maui --framework net10.0-android
```

---

## Known Limitations (Phase 1)

- **No auth**: API is open to LAN (use Phase 2 for Tailscale + API key)
- **Delayed prices**: Quotes from Yahoo Finance (delayed ~15min)
- **Paper trading only**: No buy/sell from app yet (Phase 3)
- **Manual API startup**: Task Scheduler setup required for auto-start
