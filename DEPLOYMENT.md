# Deployment & Operations Guide

## Phase 1: Local Development & LAN Access

### Building the API

```powershell
cd C:\Users\Craig\.claude\skills\Strategy_Auto_Trader_Mobile_UI
dotnet build src/MobileUI.Api/MobileUI.Api.csproj
```

### Running the API Locally

```powershell
dotnet run --project src/MobileUI.Api/MobileUI.Api.csproj
```

The API will start on `http://0.0.0.0:5000` (all interfaces).

### Testing API Endpoints

From browser on same LAN:
- Positions: `http://192.168.1.100:5000/api/positions`
- Health: `http://192.168.1.100:5000/api/health`
- Recent Trades: `http://192.168.1.100:5000/api/trades/recent?count=5`

> Replace `192.168.1.100` with the PC's actual IP on your LAN.

### Building MAUI App

```powershell
dotnet build src/MobileUI.Maui/MobileUI.Maui.csproj
```

### Running MAUI on Windows

```powershell
dotnet run --project src/MobileUI.Maui/MobileUI.Maui.csproj
```

### Running MAUI on Android Emulator

```powershell
dotnet run --project src/MobileUI.Maui/MobileUI.Maui.csproj -f net10.0-android
```

---

## Configuring the API Server IP in MAUI

Edit [src/MobileUI.Maui/Services/ApiClient.cs](src/MobileUI.Maui/Services/ApiClient.cs):

```csharp
private string _baseUrl = "http://192.168.1.100:5000";  // Change IP to match your PC
```

---

## Phase 1: Task Scheduler Auto-Start (Windows PC)

The API should auto-start on Windows logon/boot so it's ready when you wake the PC.

### 1. Create a Batch Wrapper

Save to `C:\Users\Craig\start_api.bat`:

```batch
@echo off
cd /d C:\Users\Craig\.claude\skills\Strategy_Auto_Trader_Mobile_UI
dotnet run --project src/MobileUI.Api/MobileUI.Api.csproj --configuration Release
pause
```

### 2. Create a Task Scheduler Entry

1. **Open Task Scheduler** (Win+R → `taskschd.msc`)
2. **Action** → **Create Basic Task**
   - Name: `Strategy_Auto_Trader_Mobile_UI_API`
   - Trigger: **At logon** (or **At startup** for SYSTEM account)
   - Action: **Start a program**
     - Program: `C:\Windows\System32\cmd.exe`
     - Arguments: `/k C:\Users\Craig\start_api.bat`
     - Start in: `C:\Users\Craig\start_api.bat`'s directory or leave blank
3. **Conditions**: uncheck "Stop if on batteries"
4. **Settings**: check "Restart if task ends unexpectedly", set restart interval to 1 minute

### 3. Verify Auto-Start

Restart Windows. The API should be running automatically.

---

## Phase 2: HTTPS & Tailscale (Secure Remote Access)

*(Not yet implemented — covered in Phase 2 plan)*

---

## Troubleshooting

### API won't start
- Check if port 5000 is already in use: `netstat -ano | findstr :5000`
- Check logs in `src/MobileUI.Api/appsettings.json` for paths to Strategy_Auto_Trader repo

### MAUI can't reach API
- Verify API is running: `http://192.168.1.100:5000/api/health`
- Check PC firewall allows port 5000
- Verify MAUI app has correct IP in `ApiClient.cs`
- Check network connectivity (same WiFi network)

### Health endpoint returns daemon_not_running
- Start the Strategy_Auto_Trader daemon: `python -m live_daemon`
- Wait ~60s for `app_status.json` to be written
- Refresh MAUI app

### Test suite fails
```powershell
dotnet test tests/MobileUI.Api.Tests/ -v normal
```
Check output for specific assertion failures.

---

## Configuration Files

### API Configuration

[src/MobileUI.Api/appsettings.json](src/MobileUI.Api/appsettings.json) — configure paths to Strategy_Auto_Trader:

```json
{
  "DaemonState": {
    "StatusPath": "C:\\path\\to\\Strategy_Auto_Trader\\state\\app_status.json",
    "JournalPath": "C:\\path\\to\\Strategy_Auto_Trader\\data\\journals\\live.csv"
  }
}
```

---

## Release Build

For production (Task Scheduler):

```powershell
dotnet publish src/MobileUI.Api/MobileUI.Api.csproj -c Release -o publish/api
```

Then update the batch wrapper to use `publish/api/MobileUI.Api.exe`.
