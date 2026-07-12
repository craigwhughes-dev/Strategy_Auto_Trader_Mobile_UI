# Phase 2: Secure Remote Access via Tailscale + Auth

**Goal:** Access your trading positions from anywhere (mobile data + Tailscale), with the API protected by authentication and HTTPS.

---

## Prerequisites

- Phase 1 API + MAUI app built and working on home WiFi
- Tailscale account (free: https://tailscale.com)
- Windows PC with Tailscale (install from https://tailscale.com/download/windows)
- Android phone with Tailscale (install from Google Play)
- OpenSSL for certificate generation (Windows: use WSL or download from https://slproweb.com/products/Win32OpenSSL.html)

---

## Step 1: Tailscale Setup (PC)

### 1a. Install Tailscale on PC

```bash
# Download and run installer from: https://tailscale.com/download/windows
# Or via Chocolatey:
choco install tailscale
```

Restart the installer after install.

### 1b. Sign in to Tailscale

1. Click the Tailscale icon in system tray
2. Select "Sign in"
3. Authorize with your account
4. The PC joins your tailnet

### 1c. Find your PC's Tailscale IP

```powershell
# In PowerShell:
$tailscaleIp = (tailscale ip -4)
Write-Host "PC Tailscale IP: $tailscaleIp"
```

Note this IP (typically `100.x.y.z`). Example: `100.123.45.67`

### 1d. Enable subnet routes (optional, for network-wide access)

If you want the phone to access other devices on your home network, enable subnet routing in the Tailscale admin console: https://login.tailscale.com/admin/machines

---

## Step 2: Certificate Generation & Installation

### 2a. Generate Self-Signed Certificate

Use this PowerShell script (save as `generate-cert.ps1`):

```powershell
param(
    [string]$CommonName = "strategy-api.local",
    [string]$CertPath = "C:\Users\$env:USERNAME\cert.pfx",
    [int]$DaysValid = 365
)

Write-Host "Generating self-signed certificate for: $CommonName"

# Generate private key and certificate
$cert = New-SelfSignedCertificate `
    -CertStoreLocation Cert:\CurrentUser\My `
    -DnsName $CommonName `
    -Subject "CN=$CommonName" `
    -FriendlyName "Strategy Auto Trader API" `
    -NotAfter (Get-Date).AddDays($DaysValid) `
    -HashAlgorithm SHA256 `
    -Type Custom `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")

Write-Host "Certificate created: $($cert.Thumbprint)"
Write-Host "Thumbprint: $($cert.Thumbprint)"

# Export to PFX (password-protected)
$password = Read-Host "Enter password for certificate (or press Enter for no password)" -AsSecureString
if ([string]::IsNullOrEmpty($password.ToString())) {
    $password = New-Object System.Security.SecureString
}

Export-PfxCertificate `
    -Cert $cert `
    -FilePath $CertPath `
    -Password $password

Write-Host "Certificate exported to: $CertPath"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Add thumbprint to appsettings.json:"
Write-Host "   `"CertificateThumbprint`": `"$($cert.Thumbprint)`""
Write-Host "2. Store certificate password in user secrets:"
Write-Host "   dotnet user-secrets set `"Security:CertificatePassword`" `"<password>`""
Write-Host "3. Install cert in MAUI via the settings screen (Phase 2 later step)"
```

Run it:

```powershell
# Run as Administrator
.\generate-cert.ps1 -CommonName "strategy-api.local" -DaysValid 365
```

**Note the thumbprint** — you'll need it in the next step.

### 2b. Store Certificate Thumbprint

Update `appsettings.json`:

```json
{
  "Security": {
    "CertificateThumbprint": "<thumbprint-from-step-2a>"
  }
}
```

Or set environment variable:

```powershell
$env:CERTIFICATE_THUMBPRINT = "<thumbprint-from-step-2a>"
```

### 2c. Verify Certificate

```powershell
# List certificates by thumbprint
Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq "<thumbprint>" } | Format-List
```

---

## Step 3: API Key Setup

### 3a. Generate API Key

Create a secure random key (example using PowerShell):

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($bytes)
$apiKey = [System.Convert]::ToBase64String($bytes)
Write-Host "API Key: $apiKey"
```

Save this key securely — **never commit it to git**.

### 3b. Set API Key as Environment Variable

```powershell
# Temporary (current session only):
$env:STRATEGY_API_KEY = "<api-key-from-3a>"

# Persistent (system-wide, requires Admin):
[Environment]::SetEnvironmentVariable("STRATEGY_API_KEY", "<api-key-from-3a>", "Machine")

# Persistent (user-only):
[Environment]::SetEnvironmentVariable("STRATEGY_API_KEY", "<api-key-from-3a>", "User")
```

### 3c. Verify API Key is Set

```powershell
$env:STRATEGY_API_KEY
```

Should return your API key (masked in production).

---

## Step 4: Tailscale IP Configuration (Production)

### 4a. Find PC's Tailscale IP Again

```powershell
tailscale ip -4
# Example output: 100.123.45.67
```

### 4b. Update appsettings.Production.json

```json
{
  "Security": {
    "TailscaleInterfaceIp": "100.123.45.67"
  }
}
```

Or set environment variable:

```powershell
$env:TAILSCALE_INTERFACE_IP = "100.123.45.67"
```

---

## Step 5: Phone Setup (MAUI App)

### 5a. Install Tailscale on Phone

1. Google Play Store → Search "Tailscale"
2. Install official Tailscale app
3. Open app and sign in with same account as PC

### 5b. Configure MAUI App for Tailscale

When you run the MAUI app, you'll see a settings option. Configure:

- **API URL**: `https://<PC-Tailscale-IP>:5001`
  - Example: `https://100.123.45.67:5001`
- **API Key**: Paste the key from Step 3a
- **Certificate Thumbprint**: Paste the thumbprint from Step 2a

Or edit programmatically by adding to `MauiProgram.cs`:

```csharp
if (apiClient is ApiClient concrete)
{
    concrete.SetBaseUrl("https://100.123.45.67:5001");
    await concrete.SetApiKeyAsync("<api-key-from-step-3a>");
    concrete.SetCertificateThumbprint("<thumbprint-from-step-2a>");
}
```

### 5c. Test Connection

1. Turn off WiFi on phone
2. Enable mobile data
3. Open MAUI app
4. Verify positions/trades load from API

If connection fails:
- Check Tailscale is running on both devices (check system tray)
- Verify both devices are in the same tailnet
- Check phone can ping PC: `ping 100.123.45.67` in terminal (if supported)

---

## Step 6: Testing

### 6a. API Testing (with auth)

```powershell
$apiKey = $env:STRATEGY_API_KEY
$url = "https://100.123.45.67:5001"

# Without API key → 401
curl -k "$url/api/health"

# With API key → 200
curl -k -H "X-Api-Key: $apiKey" "$url/api/health"

# Wrong key → 401
curl -k -H "X-Api-Key: wrong-key" "$url/api/health"
```

### 6b. MAUI App Testing

**On Home WiFi:**
- Change URL to `https://192.168.1.100:5001`
- Add API key
- Add certificate thumbprint
- Verify positions load

**On Mobile Data + Tailscale:**
- Change URL to `https://100.123.45.67:5001` (using Tailscale IP)
- API key and thumbprint remain the same
- WiFi OFF, mobile data ON
- Verify same positions load

**Audit Logging:**
- Check audit log file: `C:\Users\Craig\.claude\skills\Strategy_Auto_Trader_Mobile_UI\logs\audit.log`
- Should show all API requests with timestamp, IP, method, path, status

---

## Step 7: Verification Checklist

- [ ] Tailscale installed on PC and phone
- [ ] PC shows Tailscale IP (e.g., `100.123.45.67`)
- [ ] Phone joins same tailnet
- [ ] Certificate generated and thumbprint noted
- [ ] Certificate thumbprint in `appsettings.json`
- [ ] API key generated and in `STRATEGY_API_KEY` env var
- [ ] Tailscale IP in `appsettings.Production.json`
- [ ] `dotnet run` on API (development) works with local WiFi
- [ ] API responds to requests with valid API key
- [ ] API returns 401 without API key (for POST/DELETE)
- [ ] Phone can reach API on Tailscale IP via mobile data
- [ ] Audit log file created and has entries
- [ ] MAUI app shows positions from Tailscale access

---

## Troubleshooting

### Certificate Not Found

**Error:** "Warning: Certificate not found. HTTPS disabled."

**Fix:**
1. Re-run the certificate generation script
2. Verify thumbprint matches `appsettings.json`
3. Check certificate is in `Cert:\CurrentUser\My`:
   ```powershell
   Get-ChildItem Cert:\CurrentUser\My | Format-Table Thumbprint, Subject
   ```

### API Key Not Configured

**Error:** "STRATEGY_API_KEY environment variable not set"

**Fix:**
1. Set environment variable (see Step 3b)
2. Restart the API after setting env var
3. Verify: `Write-Host $env:STRATEGY_API_KEY`

### Can't Reach PC from Phone

**Checklist:**
1. Both devices logged into Tailscale
2. Both connected to internet (WiFi or mobile)
3. Check tailnet status: https://login.tailscale.com/admin/machines
4. Try pinging PC IP from phone (if terminal available)
5. Check firewall isn't blocking port 5001
6. Restart Tailscale on both devices

### Certificate Pinning Fails

**Error:** "A certificate was rejected" or "RemoteCertificateChainErrors"

**Fix:**
1. Verify thumbprint in MAUI matches actual certificate
2. Re-generate certificate if thumbprint is unclear
3. Check certificate expiry: `$cert.NotAfter`
4. If expired, re-run certificate generation with longer `DaysValid`

### Audit Log Not Created

**Fix:**
1. Ensure `Audit:LogFilePath` is set in `appsettings.json`
2. Verify directory exists: `C:\Users\Craig\.claude\skills\Strategy_Auto_Trader_Mobile_UI\logs`
3. Create if missing: `mkdir logs`
4. Check file permissions (process must be able to write)

---

## Production Deployment

When deploying to a real server (vs home PC):

1. **Use a proper certificate** from a CA (e.g., Let's Encrypt) instead of self-signed
2. **Never commit secrets** — use environment variables or secure vaults
3. **Rotate API keys** periodically
4. **Monitor audit logs** for unauthorized access attempts
5. **Firewall rules** — only allow Tailscale traffic to 5001
6. **HTTPS only** in production — disable HTTP if not needed

---

## What's Next: Phase 3

- Sell command queue (`POST /api/trades/sell`)
- Market-closed queuing until next market open
- Confirmation dialogs in MAUI
- Daemon integration to execute commands

Phase 3 depends on Phase 2 (auth) being in place.

---

## References

- Tailscale docs: https://tailscale.com/kb/
- ASP.NET Core Kestrel: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel
- Self-signed certificates: https://learn.microsoft.com/en-us/dotnet/core/additional-tools/self-signed-certificates-guide
- MAUI security: https://learn.microsoft.com/en-us/dotnet/maui/data-binding/keyboard

