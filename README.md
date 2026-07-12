# Strategy_Auto_Trader_Mobile_UI

NOT YET FINISHED - STILL UNDER DEVELOPMENT

Android MAUI app + ASP.NET Core API for remotely monitoring and controlling your Strategy_Auto_Trader positions. View live positions, closed trade history, and daemon health from your phone—on home WiFi (Phase 1) or remotely via Tailscale (Phase 2+).

## Status

### Phase 1: Read-Only API + Android App (Home WiFi) ✅ COMPLETE

**Implementation:**
- ✅ C# solution with API, MAUI app, and test projects
- ✅ API endpoints: `/api/positions`, `/api/health`, `/api/trades/recent`
- ✅ Services: StatusReader (reads `app_status.json`), JournalReader (CSV parsing), PriceFetcher (Yahoo Finance with GBX→GBP normalization, 60s cache)
- ✅ MAUI UI: daemon status banner, positions list, recent trades, pull-to-refresh, P&L coloring
- ✅ Tests: 16 passing (StatusReader, JournalReader, PriceFetcher)
- ✅ LAN deployment: binds to `0.0.0.0:5000` for all interfaces
- ✅ Ops documentation: Task Scheduler auto-start guide

**Quick Start:**
```powershell
# Run API on Windows PC
dotnet run --project src/MobileUI.Api

# Run MAUI on Windows or Android
dotnet run --project src/MobileUI.Maui -f net10.0-android

# Run tests
dotnet test tests/MobileUI.Api.Tests/
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for full setup, Task Scheduler auto-start, and troubleshooting.

### Phase 2: Secure Remote Access (Tailscale + HTTPS + Auth) ✅ COMPLETE

- ✅ Self-signed HTTPS certificate (valid 3 years, thumbprint: 7618F28C90EE396840E9B980773F8A69147E86CC)
- ✅ API key authentication middleware (checks X-Api-Key header, 401 on missing/invalid)
- ✅ Audit logging middleware (logs all requests with timestamp, IP, method, path, status)
- ✅ MAUI certificate pinning (validates server cert thumbprint)
- ✅ Tests: 4 auth middleware tests (missing key, valid key, invalid key, unprotected endpoint)
- ⏳ Tailscale setup docs (configure on PC + phone, no router port-forwarding needed)

### Phase 3: Sell Orders + Queuing ✅ C# COMPLETE (Python daemon pending)

**C# Implementation Complete:**
- ✅ `POST /api/trades/sell` — create single sell command
- ✅ `POST /api/trades/sell-all` — create sell-all command
- ✅ `GET /api/trades/commands` — list pending commands
- ✅ `GET /api/trades/commands/{id}` — get command status
- ✅ `DELETE /api/trades/commands/{id}` — cancel pending command
- ✅ CommandManager service (atomic writes, queuing, expiry)
- ✅ Command queue directory structure (`pending/`, `processing/`, `results/`, `done/`)
- ✅ Atomic file writes (write-temp-then-rename)

**Awaiting Python Daemon (Strategy_Auto_Trader repo):**
- ⏳ `process_manual_commands()` in `live_daemon.py` (polls command queue, executes sells, writes results)
- ⏳ Market-closed queueing logic (persist queued commands, execute at next open)
- ⏳ MAUI sell confirmation dialog + pending commands list
- ⏳ e2e testing with paper orders

### Phase 4: Nice-to-Haves ⏳ PENDING

- HTML notification viewing (replicate batch.py's daily_summary.html)
- Buy triggers (with kelly fraction sizing)
- IBKR live prices (instead of delayed Yahoo)
- Push notifications on execution

---

## Architecture

```
Phone (MAUI app)
  │  HTTPS + API key + Tailscale
  ▼
PC (Kestrel API sidecar)
  │  reads
  ▼
state/app_status.json (daemon heartbeat, positions, flags)
data/journals/live.csv (closed trades)
```

**Key Principles:**
- API is a **read-only sidecar** (never modifies execution_state.json or daemon_state.json)
- Single writer: daemon only
- Phase 3 uses command queue files (`state/commands/pending/`), not direct writes
- All paths configurable in `appsettings.json`

---

## Implementation Summary

### Completed ✅

**Phase 1: Read-Only API + Android App (Home WiFi)**
- Full ASP.NET Core Minimal API with Kestrel binding to 0.0.0.0:5000
- Services: StatusReader (app_status.json), JournalReader (live.csv), PriceFetcher (Yahoo Finance with GBX→GBP, 60s cache)
- MAUI app: positions list, recent trades, daemon status banner, pull-to-refresh, P&L coloring
- 16 passing unit tests (StatusReader, JournalReader, PriceFetcher, MAUI integration)
- LAN deployment guide: PC IP configuration, MAUI ApiClient setup

**Phase 2: Secure Remote Access (Tailscale + HTTPS + Auth)**
- Self-signed HTTPS certificate (valid 3 years, thumbprint: 7618F28C90EE396840E9B980773F8A69147E86CC)
- API key auth middleware (X-Api-Key header, 401 on invalid/missing)
- Audit logging middleware (all requests logged with timestamp, IP, method, path, status)
- MAUI certificate pinning (validates server cert thumbprint before accepting responses)
- 4 passing auth middleware tests

**Phase 3: Sell Orders (C# Backend)**
- CommandManager service: atomic file writes, command queue management
- 5 API endpoints: POST /sell, POST /sell-all, GET /commands, GET /commands/{id}, DELETE /commands/{id}
- Command directory structure: pending/, processing/, results/, done/
- Validation: checks ticker exists before creating sell command
- Error handling: 400 on invalid ticker, 409 on already-executing commands

### Remaining ⏳

**Phase 3 Continuations (Python Daemon Integration)**
- `process_manual_commands()` implementation in live_daemon.py: poll command queue, execute sells, write results
- Market-closed queueing: persist queued commands in pending/, execute at next market open
- MAUI UI: sell confirmation dialog, pending commands list, progress polling
- Tests: Python tests for command processing, e2e with paper orders

**Phase 4 (Nice-to-Haves)**
- HTML notification viewing: GET /api/notifications/{ticker}/latest
- Buy triggers: mirror sell command structure with kelly fraction sizing
- IBKR live prices: replace Yahoo delayed quotes
- Push notifications on execution

---

## Project Structure

```
src/
  MobileUI.Api/
    Program.cs
    Endpoints/PositionsEndpoints.cs
    Services/{StatusReader,JournalReader,PriceFetcher}.cs
    Models/{Position,DaemonStatus,TradeRecord}.cs
    appsettings.json (paths to Strategy_Auto_Trader)
    Properties/launchSettings.json (0.0.0.0:5000)
  MobileUI.Maui/
    MauiProgram.cs
    Services/ApiClient.cs (HttpClient wrapper, configurable base URL)
    ViewModels/PositionsViewModel.cs (pull-to-refresh, error handling)
    Views/PositionsPage.xaml (UI layout, bindings)
    Models/{Position,TradeRecord,DaemonStatus}.cs
tests/
  MobileUI.Api.Tests/
    Services/{StatusReaderTests,JournalReaderTests,PriceFetcherTests}.cs
  MobileUI.Maui.Tests/
    (UI-only tests require platform-specific setup; logic in ViewModels)
```

---

## Development

### Building

```powershell
dotnet build  # All projects
dotnet build src/MobileUI.Api/MobileUI.Api.csproj  # API only
dotnet build src/MobileUI.Maui/MobileUI.Maui.csproj  # MAUI only
```

### Testing

```powershell
dotnet test tests/MobileUI.Api.Tests/  # Unit tests (16 tests)
```

### Configuration

**API paths** ([appsettings.json](src/MobileUI.Api/appsettings.json)):
```json
{
  "DaemonState": {
    "StatusPath": "C:\\path\\to\\Strategy_Auto_Trader\\state\\app_status.json",
    "JournalPath": "C:\\path\\to\\Strategy_Auto_Trader\\data\\journals\\live.csv"
  }
}
```

**MAUI server** ([ApiClient.cs](src/MobileUI.Maui/Services/ApiClient.cs)):
```csharp
private string _baseUrl = "http://192.168.1.100:5000";  // Your PC's LAN IP
```

---

## Next Steps (Tomorrow/Phase 3 Completion)

1. **Python Daemon Command Loop** — In Strategy_Auto_Trader repo:
   - Implement `process_manual_commands()` in `live_daemon.py`
   - Poll `state/commands/pending/` every ~60s (same as main poll loop, not hourly)
   - Move file `pending/{id}.json` → `processing/` (atomic claim via rename)
   - Execute sell via existing `PortfolioManager.record_exit()` + `IBKRAdapter.place_order()`
   - Write result to `results/{id}.json` (status: filled/error, fill_price, or error_message)
   - Test: verify a paper sell from MAUI fills in TWS + `execution_state.json`

2. **MAUI Sell UI** — In this repo:
   - Position row "Sell" button → confirmation dialog (ticker, qty, estimated value, market status)
   - If market closed: "Will execute at next open: HH:MM UTC" message
   - POST /sell → poll GET /commands/{id} until status != pending
   - Toast with fill price or error
   - Pending commands section: list all commands with status, cancel buttons

3. **e2e Testing** (paper orders only):
   - Verify a sell from the phone → appears in `/api/trades/commands` → executes → appears in `/api/trades/recent`
   - Verify market-closed sell queues → executes at next open → no error
   - Verify queued sell can be cancelled before open

4. **Deployment Verification**:
   - Task Scheduler auto-start (start daemon, then API, verify both auto-recover on crash)
   - Tailscale setup: add PC + phone to tailnet, test app via Tailscale IP + HTTPS + API key
   - No-key requests return 401 ✓

---

## References

- Full implementation plan: [plan file](https://github.com/anthropics/claude-code/blob/main/docs/ticklish-prancing-marble.md)
- Deployment & ops guide: [DEPLOYMENT.md](DEPLOYMENT.md)
- Python Phase 0 enablers: [Strategy_Auto_Trader](https://github.com/your-org/Strategy_Auto_Trader) repo (atomic writes, market/currency fields, app_status.json heartbeat)
