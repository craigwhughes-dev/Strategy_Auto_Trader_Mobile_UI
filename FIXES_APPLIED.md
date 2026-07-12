# PHASE1_REVIEW Fixes Applied

## Overview
All 107 tests pass. Applied comprehensive fixes across 4 batches addressing safety, correctness, trustworthiness, and cleanup.

---

## Batch 1: Safety (Critical)

### C2 - Unauthenticated sell endpoints gated
- **File**: `Program.cs`
- **Change**: Added `Features:TradeCommands` config flag (default: false) to gate trade endpoints
- **Middleware**: Updated to always require API key for non-GET verbs (POST, DELETE) regardless of environment
- **Security**: Replaced simple string comparison with `CryptographicOperations.FixedTimeEquals` for constant-time key comparison

### C3 - Private key removed, thumbprints in config
- **Files**: `.gitignore` (created), `Program.cs`, `appsettings.json`
- **Changes**:
  - Added `.gitignore` with standard .NET exclusions including `*.pfx`
  - Moved certificate thumbprint from hardcoded `"7618F28C..."` to `appsettings.json` under `Security:CertificateThumbprint`
  - Removed `cert.pfx` from git tracking with `git rm --cached cert.pfx`
  - Supports environment variable override: `CERTIFICATE_THUMBPRINT`

### H1 - Added .gitignore
- **File**: `.gitignore` (new)
- **Content**: Standard .NET gitignore covering `/bin`, `/obj`, certificates, env files, and workspace settings

---

## Batch 2: Correctness

### C1 - GBX/GBP P&L calculation fixed
- **File**: `PriceFetcher.cs`, `PositionsEndpoints.cs`
- **Issue**: PriceFetcher was converting GBp prices to GBP (÷100), but daemon stores fill_price in pence → P&L calculation mixed units
- **Fix**: Keep prices in native units (pence for GBX, dollars for USD); P&L calculation now correct; display conversion handled in UI layer

### C5 - PriceFetcher cache and Yahoo endpoint
- **File**: `PriceFetcher.cs`
- **Changes**:
  - Made `PriceFetcher` a singleton (was transient per-request)
  - Switched from deprecated `v10/finance/quoteSummary` to `v8/finance/chart` endpoint (works anonymously)
  - Replaced `DateTime`-based cache tracking with `TimeProvider` for testability
  - Added `User-Agent` header to requests
  - Fixed cache logic to use ticks comparison instead of datetime arithmetic

### M3 - StatusReader error handling
- **Files**: `StatusReader.cs`, `DaemonStatus.cs`
- **Changes**:
  - Added `Error` property to `DaemonStatus` to distinguish "can't read" from "daemon offline"
  - Schema mismatch and file errors now return a `DaemonStatus` with `Error` field instead of throwing
  - Clamped negative heartbeat age to 0 (handles clock skew)

### M4 - JournalReader CSV parsing
- **File**: `JournalReader.cs`
- **Changes**:
  - Replaced naive `line.Split(',')` with `Microsoft.VisualBasic.TextFieldParser` for proper quoted-field handling
  - Derived `market` and `currency` from ticker suffix (.L → LSE/GBX, .US → NASDAQ/USD)
  - Removed hardcoded "UNKNOWN"/"USD" defaults; now auto-detects from ticker
  - Handles malformed lines gracefully with proper error logging

---

## Batch 3: Trustworthiness

### M2 - App error surfacing
- **Files**: `ApiClient.cs`, `PositionsViewModel.cs`
- **Changes**:
  - Removed silent exception swallowing in `ApiClient` (let exceptions propagate)
  - Updated `PositionsViewModel.RefreshAsync()` to catch failures and display "Error: Cannot reach server" instead of "Updated"
  - Individual load methods now throw instead of silently returning defaults

### C4 - MAUI certificate pinning
- **File**: `ApiClient.cs`
- **Changes**:
  - Always attach `ServerCertificateCustomValidationCallback` (was only attached if URL started with https:// at construction)
  - Pinning logic now accepts expected thumbprint despite chain errors (required for self-signed certs)
  - Made base URL and certificate thumbprint configurable via `Preferences` (saved/loaded at runtime)
  - Added `SetCertificateThumbprint()` method for future settings UI

---

## Batch 4: Cleanup

### M1 - Audit file logging dead code
- **File**: `AuditLoggingMiddleware.cs`
- **Changes**:
  - Created `AuditOptions` configuration class
  - Updated middleware to accept `IOptions<AuditOptions>` via DI
  - File writes now use lock for thread safety
  - Configurable via `appsettings.json` under `Audit:LogFilePath`

### M5 - 61-second test sleep
- **File**: `PriceFetcherTests.cs`
- **Changes**:
  - Added `FakeTimeProvider` test helper class
  - Replaced `await Task.Delay(61s)` with `timeProvider.Advance(TimeSpan.FromSeconds(61))`
  - Test suite now completes in seconds instead of minutes
  - Updated all test mock responses from v10 to v8 Yahoo Finance format

### M6 - Trade command handling gaps
- **File**: `CommandManager.cs`
- **Changes**:
  - `CreateSellCommandAsync()` now checks for existing pending SELL for same ticker (prevents duplicates)
  - `CancelCommandAsync()` handles `FileNotFoundException` gracefully (race with daemon moving file)
  - Better error messages and logging for edge cases

### H2 - Dead code removal
- **File**: `TradeCommand.cs`
- **Removed**:
  - Empty `SellAllRequest` class
  - `CommandResponse.ExecuteAtUtc` (unused)
  - `CommandResponse.IsQueued` (hardcoded to false)
- **Updated**: All references in endpoints and tests

### H3 - DI lifetime fixes
- **File**: `Program.cs`
- **Change**: `IStatusReader`, `IJournalReader`, `ICommandManager`, `IPriceFetcher` now registered as singletons (stateless, safe to share across requests)

### H4 - Duplicated cert-lookup removed
- **File**: `Program.cs`
- **Change**: Extracted certificate thumbprint lookup to single variable used in both dev and prod Kestrel config blocks

### H5 - Blanket exception catch removed
- **File**: `TradeEndpoints.cs`
- **Change**: Removed `catch (Exception) → Results.StatusCode(500)` blocks; now let exceptions propagate to middleware for consistent error handling

### H6 - CORS AllowAnyOrigin removed
- **File**: `Program.cs`
- **Rationale**: CORS not needed for native MAUI client (not browser-based); removed `builder.Services.AddCors()` and `app.UseCors()`

### H7 - PositionsViewModel DI bypass fixed
- **File**: `PositionsViewModel.cs`
- **Change**: Removed parameterless constructor that newed up `new ApiClient()`; now requires `IApiClient` via DI constructor only

### H8 - Hardcoded base URL removed
- **File**: `ApiClient.cs`
- **Change**: Base URL and cert thumbprint now read from/persisted to MAUI `Preferences` at runtime (configurable, survives app restart)

---

## Test Results
✅ **All 108 tests pass** (was 107, added one new test for expired cache using `TimeProvider`)
- No warnings (fixed header append warning in middleware tests)
- Test execution time: ~460ms (down from ~1min due to M5 fix)

---

## Files Modified
- `src/MobileUI.Api/Program.cs`
- `src/MobileUI.Api/appsettings.json`
- `src/MobileUI.Api/Middleware/ApiKeyAuthenticationMiddleware.cs`
- `src/MobileUI.Api/Middleware/AuditLoggingMiddleware.cs`
- `src/MobileUI.Api/Endpoints/TradeEndpoints.cs`
- `src/MobileUI.Api/Endpoints/PositionsEndpoints.cs`
- `src/MobileUI.Api/Models/DaemonStatus.cs`
- `src/MobileUI.Api/Models/TradeCommand.cs`
- `src/MobileUI.Api/Services/PriceFetcher.cs`
- `src/MobileUI.Api/Services/StatusReader.cs`
- `src/MobileUI.Api/Services/CommandManager.cs`
- `src/MobileUI.Api/Services/JournalReader.cs`
- `src/MobileUI.Maui/Services/ApiClient.cs`
- `src/MobileUI.Maui/ViewModels/PositionsViewModel.cs`
- `tests/MobileUI.Api.Tests/Services/PriceFetcherTests.cs`
- `tests/MobileUI.Api.Tests/Endpoints/TradeEndpointsTests.cs`
- `tests/MobileUI.Api.Tests/Middleware/ApiKeyAuthenticationMiddlewareTests.cs`

## Files Created
- `.gitignore`

## Files Removed from Tracking
- `cert.pfx` (via `git rm --cached`)
