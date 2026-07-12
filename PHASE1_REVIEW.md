# Phase 1 Code Review & Fix Plan

Review date: 2026-07-12. Scope: everything on `main` (Phase 1 implementation). All 107 tests pass, but several correctness and security issues exist — some contradict the claims in PHASE1_COMPLETE.md.

---

## Critical findings

### C1. GBX/GBP unit mismatch → wrong P&L for FTSE positions
- `PriceFetcher.FetchSinglePriceAsync` converts Yahoo `GBp` prices to **pounds** (`regularPrice /= 100`).
- The daemon stores `fill_price` for `.L` tickers in **pence** (see `Strategy_Auto_Trader/broker/symbols.py` — "LSE prices (yfinance and IBKR alike) are quoted in pence").
- `PositionsEndpoints.GetPositions` computes `UnrealizedPnl = (price - FillPrice) * Quantity`, mixing GBP with GBX → every FTSE position shows a ~99% loss.
- **Never actually verified**: `app_status.json` currently has zero positions, so the "positions with prices" checklist item never exercised this path with real data.
- **Fix**: use `Position.Currency` (already parsed). Keep Yahoo price in pence for GBX positions (drop the /100), compute P&L in native units, and convert to GBP only for display (`pnl / 100` when currency == GBX). Add BVA tests for a GBX position.

### C2. Unauthenticated sell endpoints are live in Phase 1
- PHASE1_COMPLETE.md claims "no buy/sell control yet", but `Program.cs` maps `MapTradeEndpoints()`: `POST /api/trades/sell`, `POST /api/trades/sell-all`, `DELETE /api/trades/commands/{id}`.
- `ApiKeyAuthenticationMiddleware` is only registered when **not** Development. `dotnet run` (and the Task Scheduler script as written) runs Development → anyone on the LAN can queue `SELL_ALL` against the live daemon, bound to `0.0.0.0`.
- **Fix (pick one)**:
  - Don't map trade endpoints until Phase 3 (config flag `Features:TradeCommands`, default off), **and/or**
  - Always require the API key for non-GET verbs regardless of environment.

### C3. Private key committed to the repo
- `cert.pfx` is committed, and its thumbprint `7618F28C…` is hardcoded in both `Program.cs` and the MAUI `ApiClient`. Anyone with repo access holds the server's private key, which defeats the pinning it exists to support.
- **Fix**: delete `cert.pfx` from the working tree, regenerate the cert, move thumbprints to config (`appsettings` / env var for API, MAUI preferences or build-time config for the app). History rewrite optional since the repo is local-only — regenerating the cert is what actually fixes it.

### C4. MAUI certificate pinning never activates
- `ApiClient` attaches `ServerCertificateCustomValidationCallback` only if `_baseUrl` starts with `https://` **at construction** — but the field initializer is `http://192.168.1.100:5000`, so the callback is never attached; `SetBaseUrl` doesn't rebuild the handler.
- Also: the callback rejects any `SslPolicyErrors != None`, which will always be the case for a self-signed cert — so even if attached, HTTPS would never succeed. Pinning logic must *accept* the expected thumbprint despite chain errors.
- **Fix**: always attach the callback; accept iff thumbprint matches (ignore chain errors for that exact cert); make base URL + thumbprint user-configurable (settings page or `Preferences`), not hardcoded.

### C5. PriceFetcher cache is per-request (useless) and Yahoo endpoint likely broken
- `AddHttpClient<IPriceFetcher, PriceFetcher>()` registers a **transient** typed client → a fresh `_priceCache` per request; the 60s cache never hits in production. The dictionary is also not thread-safe.
- Yahoo's `v10/finance/quoteSummary` endpoint has required a cookie+crumb since ~2023 and typically returns 401/429 for anonymous callers. With zero live positions this was never actually exercised.
- **Fix**: make the fetcher a singleton using `IHttpClientFactory` (or inject `IMemoryCache`); switch to the `v8/finance/chart/{ticker}` endpoint (works anonymously, returns `meta.regularMarketPrice` + `meta.currency`) with a `User-Agent` header; verify against a real `.L` ticker.

---

## Moderate findings

### M1. Audit file logging is dead code
`AuditLoggingMiddleware` takes `string auditLogPath = ""` — `UseMiddleware` never supplies it, so file auditing never happens (and DI resolution of a string ctor param is fragile). **Fix**: bind an `AuditOptions` from configuration; write via `File.AppendAllText` under a lock or use the logger's file provider.

### M2. App shows "Updated" when everything failed
`ApiClient` swallows all exceptions and returns empty lists/defaults, so `PositionsViewModel.RefreshAsync`'s catch is unreachable and `StatusMessage = "Updated"` displays even when the API is unreachable. A trader can't distinguish "flat, all good" from "app can't reach the server". **Fix**: let `ApiClient` throw (or return a result type); surface per-call failure in `StatusMessage` and an offline banner.

### M3. StatusReader conflates errors with "daemon offline"
- Schema mismatch throws, then its own catch swallows it and returns `DaemonRunning = false` — UI reports the daemon down when it's running fine.
- **Fix**: add an `Error`/`StatusSource` field to `DaemonStatus` so "can't read status" is distinct from "heartbeat stale". Clamp negative heartbeat age (clock skew) to 0.

### M4. JournalReader CSV parsing is naive
- `line.Split(',')` breaks on quoted fields — the journal's `notes` column will eventually contain commas and shift every subsequent field.
- Maps `market`/`currency` columns that **don't exist** in `live.csv` → always "UNKNOWN"/"USD", wrong labeling for FTSE trades (`pnl_usd` is actually pot-currency GBP).
- **Fix**: minimal quoted-CSV parser (or `Microsoft.VisualBasic.TextFieldParser`/small helper) + derive market/currency from ticker suffix like the daemon does; rename `RoundtripPnl` presentation to pot currency.

### M5. Real 61-second sleep in tests
`PriceFetcherTests.cs:135` does `await Task.Delay(TimeSpan.FromSeconds(61))` to test cache expiry — the whole suite takes ~1 min because of it. **Fix**: inject `TimeProvider` into `PriceFetcher`; use `FakeTimeProvider` in tests.

### M6. Trade command handling gaps (matters when Phase 3 goes live)
- Duplicate pending SELLs for the same ticker aren't rejected.
- `CancelCommandAsync` has a check-then-delete race with the daemon moving the file to `processing/` (small window; `File.Delete` on a just-moved file throws unhandled).
- API key comparison isn't constant-time — use `CryptographicOperations.FixedTimeEquals`.

---

## Hygiene / refactoring

| # | Item | Fix |
|---|------|-----|
| H1 | No `.gitignore`; `bin/`, `obj/` committed (most of the repo churn) | Add standard .NET gitignore; `git rm -r --cached` the artifact dirs |
| H2 | Dead code: no-op ternary `ticker.Contains('.') ? ticker : ticker` in PriceFetcher; empty `SellAllRequest`; `CommandResponse.IsQueued`/`ExecuteAtUtc` hardcoded/unused | Delete |
| H3 | DI lifetimes: readers + `CommandManager` are stateless → register singleton; `CommandManager` re-creates directories every scope | Singleton; create dirs once |
| H4 | Duplicated cert-lookup block in `Program.cs` dev/prod branches | Extract one helper, thumbprint from config in both |
| H5 | `catch (Exception) → Results.StatusCode(500)` in TradeEndpoints hides all diagnostics | Drop the blanket catch; add exception-handler middleware |
| H6 | CORS `AllowAnyOrigin` serves no purpose for a native MAUI client | Remove the CORS policy |
| H7 | `PositionsViewModel` parameterless ctor news up `ApiClient`, bypassing DI | Remove; resolve via DI only |
| H8 | Hardcoded `http://192.168.1.100:5000` default in ApiClient | Config/preferences with a settings UI (fold into C4 work) |

---

## Suggested fix order

**Batch 1 — safety (do before the API runs unattended via Task Scheduler):**
C2 (gate trade endpoints), C3 (remove cert.pfx, config thumbprints), H1 (.gitignore).

**Batch 2 — correctness of what Phase 1 actually shows:**
C1 (GBX P&L), C5 (fetcher lifetime + working Yahoo endpoint, verified against a live `.L` quote), M3 (status vs offline), M4 (CSV + currency labeling).

**Batch 3 — app trustworthiness:**
M2 (error surfacing in UI), C4 (pinning + configurable base URL).

**Batch 4 — cleanup:**
M1, M5, M6, H2–H8. Re-run full suite; suite should complete in seconds after M5.

Each batch is independently shippable; tests to be added/updated alongside each fix (BVA for the GBX/GBP boundary per house practice).
