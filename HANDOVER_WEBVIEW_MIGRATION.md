# Handover: Replace native MAUI UI with WebView

## Context

Mobile app (`src/MobileUI.Maui`) currently duplicates the web UI (`src/MobileUI.Api/wwwroot`) in native XAML/C#. The native version is broken (tier allocation list doesn't render) and every fix cycle costs ~2-3 min (native Android rebuild + APK install + adb), with silent binding failures (no exceptions, no logs) making bugs slow to diagnose. The web UI already works correctly and is trivial to iterate on (edit HTML/JS, refresh browser).

Decision: stop maintaining two UIs. Make the MAUI app a thin WebView shell pointing at the same server pages the browser already uses.

## Session rules (do not violate)

- **Never restart the live daemon** (`StrategyAutoTraderDaemon` scheduled task). Read-only on that process. If it needs a bounce, tell the user to run `C:\Users\Craig\.claude\skills\Strategy_Auto_Trader\scripts\bounce_daemon.ps1` themselves.
- **Never restart/redeploy the API process yourself.** User handles all restarts of `StrategyAutoTraderMobileAPI` scheduled task themselves. Build/publish is fine; restarting the running process is not.
- **Don't `git commit` or push** unless explicitly told to.

## Current repo state (uncommitted, on `main`)

Two independent fixes already made and verified working, sitting uncommitted:

1. `src/MobileUI.Api/Services/StatusReader.cs` — added null-guards (`vix_current`, `vxn_current`, `selected_tier_num`, `tiers` array kind check) before calling `.GetDouble()`/`.GetInt32()`. Root cause of "tier data not appearing on UI" — the daemon occasionally writes these fields as JSON `null` mid-cycle, which crashed the whole `/api/health` read (caught, returned `Error` status, dropping `TierAllocation` entirely). Verified via `dotnet build` — compiles clean. **This fix is good, keep it, eventually let the user commit it.**
2. `src/MobileUI.Maui/App.xaml` + `src/MobileUI.Maui/Converters/PnlColorConverter.cs` — added `NotNullToBoolConverter`, `GateValueConverter`, `NotEqualConverter`, `PassFailColorConverter`, which `MainPage.xaml` referenced but were never defined (this is what caused the app to crash on launch, then rendered an incomplete tier section once fixed). **These converters become dead code once the WebView migration lands — delete them along with the rest of the native tier/position UI (see Plan step 4).**

The API is deployed via `dotnet publish -c Release -o publish/api` (NOT `dotnet build` — plain build never copies `wwwroot`, only `publish` does; this cost real time to discover). Scheduled task `StrategyAutoTraderMobileAPI` runs `publish\api\MobileUI.Api.exe` with Start In `publish\api`. Task Scheduler needs elevated/admin PowerShell to query or edit (non-admin `schtasks`/`Get-ScheduledTask` returns Access Denied silently) — ask the user to run queries themselves if you hit that wall, don't loop retrying.

## Why the native UI was hard and the web UI wasn't

- Web UI (`wwwroot/index.html`, `app.js`) is plain JS/DOM, served by the API itself (`app.UseStaticFiles()`), same-origin fetches, no build step. It already renders the full tier table correctly.
- `app.js` already owns its own API key handling (`localStorage`, `X-Api-Key` header, lines ~36/485/489) — the API's `ApiKeyAuthenticationMiddleware` only requires the key on non-GET/HEAD requests (`Middleware/ApiKeyAuthenticationMiddleware.cs:22-23`), so page loads and health polling need no auth at all. This means a WebView pointed at the page needs **no native auth plumbing** — the page handles it.
- MAUI native version (`MainPage.xaml`, `PositionsViewModel.cs`, `ApiModels.cs`, `ApiClient.cs`, `Converters/PnlColorConverter.cs`) reimplements all of this in XAML bindings + C#, and XAML binding failures are silent (no crash, no log, item just doesn't render) — confirmed by adding a `Console.WriteLine` diagnostic that never appeared in `adb logcat` at all (Console output isn't reliably routed to logcat in this build config), making root-causing slow.

## Plan

1. **Replace `MainPage.xaml` content with a `WebView`.**
   - `Source` bound to the stored server URL — reuse the existing `api_base_url` Preferences key (see `ApiClient.cs:14/22`, currently `"http://localhost:5000"` default, real value e.g. `https://<tailscale-ip>:5001`).
   - Keep it simple: one `WebView` filling the page, optionally a manual refresh button that calls `webView.Reload()`.

2. **Handle the self-signed cert for the WebView.**
   - The existing `ApiClient.cs` (`ValidateServerCertificate`, lines 37-58) already bypasses/accepts the cert by thumbprint (or unconditionally if no thumbprint configured) for `HttpClient`. Android's native `WebView` does its own TLS validation and will show an interstitial/blank page on a self-signed cert unless overridden.
   - Do this via a custom Android `WebViewClient` (platform-specific handler mapping, e.g. `Handlers.WebViewHandler` override on Android, or `WebViewClient.OnReceivedSslError` calling `handler.Proceed()`) — same trust model already accepted for `ApiClient`, this is a private Tailscale-only app, not public-facing.
   - Cert thumbprint config already exists (`Security:CertificateThumbprint` in `appsettings.json`, `Preferences` key `api_cert_thumbprint` on the MAUI side) — reuse rather than re-invent.

3. **Keep `SettingsPage.xaml`** for entering/changing the server URL (drop the API-key field from it if present — the web page manages its own key now via its own in-page settings gear icon, confirmed present in the browser screenshot). On save, navigate/reload the WebView to the new URL.

4. **Delete dead native code** once the WebView renders correctly and has been visually verified on the phone:
   - `src/MobileUI.Maui/ViewModels/PositionsViewModel.cs`
   - `src/MobileUI.Maui/Converters/PnlColorConverter.cs` (all converters in it — `PnlColorConverter`, `BoolToColorConverter`, `BoolToStatusTextConverter`, `InverseBoolConverter`, `IntToBoolConverter`, `NotNullToBoolConverter`, `GateValueConverter`, `NotEqualConverter`, `PassFailColorConverter`)
   - Converter registrations in `App.xaml`
   - Tier/Position/TradeRecord/TradeCommand/CommandResponse classes in `Models/ApiModels.cs` if nothing else references them
   - `Services/ApiClient.cs` / `IApiClient` if nothing else references them (check `MauiProgram.cs` DI registration and remove it there too)
   - Don't delete `SettingsViewModel.cs`/`SettingsPage.xaml` — still needed for URL config.

5. **Build, deploy to phone, verify.**
   - Device already paired this session: `ZY22LH28VM` (Moto G85), USB debugging authorized, adb at `C:\Users\Craig\AppData\Local\Android\Sdk\platform-tools\adb.exe`.
   - Build: `dotnet build src/MobileUI.Maui -f net10.0-android -c Debug -t:Rebuild` (use `-t:Rebuild` — plain incremental `build` has repeatedly failed to pick up XAML/resource changes in this project this session, cost real debugging time twice).
   - Install: `adb -s ZY22LH28VM install -r <path-to>.apk` (APK lands at `src/MobileUI.Maui/bin/Debug/net10.0-android/com.strategyadtrader.mobileui-Signed.apk`).
   - Launch: `adb -s ZY22LH28VM shell monkey -p com.strategyadtrader.mobileui -c android.intent.category.LAUNCHER 1` (plain `am start` with a guessed activity class name fails — package uses a `crc64...MainActivity` mangled name; `monkey` with the launcher category sidesteps needing the exact name).
   - Confirm the phone WebView shows the same tier table as `https://localhost:5001/` in a desktop browser (VIX/VXN, 4 tier rows with PASS/FAIL, positions, etc.) — that's the acceptance bar, it should be a literal pixel-for-pixel match since it's the same HTML.

## Open questions for the implementing session

- Does `MauiProgram.cs` register `ApiClient`/`IApiClient` via DI? Check before deleting — removing an unresolvable DI registration will crash startup.
- Confirm nothing else in the app (e.g. a notification/background service) depends on `PositionsViewModel` or `ApiClient` before deleting them.
