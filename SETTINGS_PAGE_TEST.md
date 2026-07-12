# Settings Page Implementation - Test Guide

## What Was Built

✅ **Settings Page (SettingsPage.xaml & SettingsPage.xaml.cs)**
- Clean UI with input fields for API URL, API Key, Certificate Thumbprint
- Save Settings button - persists values locally
- Test Connection button - verifies API connectivity
- Status message display
- Help text showing how to get configuration values

✅ **Settings ViewModel (SettingsViewModel.cs)**
- Loads existing settings on page open
- Saves to MAUI Preferences (URL, thumbprint) & SecureStorage (API key - encrypted)
- Validates inputs before saving
- Test Connection queries the API to verify credentials work

✅ **App Navigation (AppShell.xaml)**
- Added Settings tab alongside Home tab
- Accessible from tab bar at bottom of app

✅ **Dependency Injection (MauiProgram.cs & ServiceHelper.cs)**
- SettingsPage & SettingsViewModel registered in DI container
- ApiClient wired up for SettingsViewModel to use

## Build Status

✅ **Build succeeded** - All code compiles without errors

## Manual Testing Checklist

Run on your Windows PC or Android phone:

### 1. App Launches
- [ ] Open the MAUI app
- [ ] Verify Home tab shows trading positions (from Phase 1)

### 2. Settings Tab Accessible
- [ ] Tap the "Settings" tab at bottom
- [ ] Verify Settings page loads with input fields

### 3. Pre-populated Values (if saved previously)
- [ ] API URL field shows previously saved value (or empty)
- [ ] Certificate Thumbprint field shows previously saved value (or empty)
- [ ] API Key field is empty (encrypted storage shows nothing for security)

### 4. Save Settings
- [ ] Enter your Tailscale IP: `https://100.x.x.x:5001`
- [ ] Paste API Key from Step 3a
- [ ] Paste Certificate Thumbprint from Step 2a
- [ ] Tap "Save Settings"
- [ ] Verify status shows "Settings saved successfully"

### 5. Settings Persist
- [ ] Close the app completely
- [ ] Reopen the app
- [ ] Go to Settings tab
- [ ] Verify API URL and Thumbprint are still there
- [ ] (API Key won't show for security, but it's stored in SecureStorage)

### 6. Test Connection
- [ ] With settings saved, tap "Test Connection"
- [ ] App should connect to API
- [ ] Verify status shows: "Connection successful! Found X positions."

### 7. API Access Changes
- [ ] Go back to Home tab
- [ ] Tap refresh or let it auto-load
- [ ] Verify positions load from the configured API URL
- [ ] (If on Tailscale mobile, turn off WiFi and use mobile data only)
- [ ] Positions should still load via Tailscale IP

## Stored Values (Private to Device)

Settings are stored using MAUI's built-in APIs:

- **API URL** → `Preferences` (device local storage, not in git)
- **Certificate Thumbprint** → `Preferences` (device local storage, not in git)
- **API Key** → `SecureStorage` (encrypted storage, not in git)

None of these values are committed to the repository.

## If Something Fails

### Settings page doesn't load
- Check Android/iOS system allows reading Preferences
- Verify app has required permissions

### Save fails
- Verify API URL format: `https://IP:5001`
- Check API Key is not empty

### Test Connection fails
- Verify Tailscale is connected on phone
- Verify PC API is running: `dotnet run` in `src/MobileUI.Api`
- Check firewall on PC isn't blocking port 5001
- Verify certificate thumbprint matches exactly (case-sensitive)

### Settings not persisting
- Check device storage is not full
- Try clearing app cache and re-entering settings

## Next Steps

After testing completes, move to **Step 6: Testing** in PHASE2_SETUP.md:
- API testing via curl with auth headers
- MAUI app testing on home WiFi
- MAUI app testing on mobile data + Tailscale
- Audit log verification
