<#
.SYNOPSIS
Generates a self-signed certificate for the Strategy Auto Trader API.

.DESCRIPTION
Creates a self-signed certificate for HTTPS, stores it in the current user's certificate store,
and displays the thumbprint needed for configuration.

.PARAMETER CommonName
The Common Name (CN) for the certificate. Default: "strategy-api.local"

.PARAMETER DaysValid
Number of days the certificate is valid. Default: 365

.EXAMPLE
.\generate-certificate.ps1 -CommonName "strategy-api.local" -DaysValid 365

#>
param(
    [string]$CommonName = "strategy-api.local",
    [int]$DaysValid = 365
)

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Certificate Generation Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Host "[WARNING] Running without administrator privileges." -ForegroundColor Yellow
    Write-Host "   You may not have permission to install the certificate." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Generating self-signed certificate..." -ForegroundColor Green
Write-Host "  Common Name: $CommonName" -ForegroundColor Gray
Write-Host "  Valid Days: $DaysValid" -ForegroundColor Gray
Write-Host "  Hash Algorithm: SHA256" -ForegroundColor Gray
Write-Host ""

try {
    # Generate certificate
    $cert = New-SelfSignedCertificate `
        -CertStoreLocation Cert:\CurrentUser\My `
        -DnsName $CommonName `
        -Subject "CN=$CommonName" `
        -FriendlyName "Strategy Auto Trader API" `
        -NotAfter (Get-Date).AddDays($DaysValid) `
        -HashAlgorithm SHA256 `
        -Type Custom `
        -KeyUsage DigitalSignature, KeyEncipherment `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")

    if (-not $cert) {
        throw "Failed to create certificate"
    }

    $thumbprint = $cert.Thumbprint
    $expiryDate = $cert.NotAfter

    Write-Host "[OK] Certificate created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Certificate Details:" -ForegroundColor Cyan
    Write-Host "  Thumbprint:  $thumbprint" -ForegroundColor White
    Write-Host "  Subject:     $($cert.Subject)" -ForegroundColor White
    Write-Host "  Expires:     $expiryDate" -ForegroundColor White
    Write-Host "  Stored in:   Cert:\CurrentUser\My" -ForegroundColor White
    Write-Host ""

    # Display configuration instructions
    Write-Host "Configuration Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Update appsettings.json with the thumbprint:" -ForegroundColor Yellow
    Write-Host "   " -NoNewline
    Write-Host '"Security": { "CertificateThumbprint": "' -ForegroundColor Gray -NoNewline
    Write-Host $thumbprint -ForegroundColor Green -NoNewline
    Write-Host '" }' -ForegroundColor Gray
    Write-Host ""

    Write-Host "2. Or set environment variable:" -ForegroundColor Yellow
    Write-Host "   " -NoNewline
    Write-Host '$env:CERTIFICATE_THUMBPRINT = "' -ForegroundColor Gray -NoNewline
    Write-Host $thumbprint -ForegroundColor Green -NoNewline
    Write-Host '"' -ForegroundColor Gray
    Write-Host ""

    Write-Host "3. Verify the certificate is installed:" -ForegroundColor Yellow
    Write-Host "   " -ForegroundColor Gray -NoNewline
    Write-Host "Get-ChildItem Cert:\CurrentUser\My | Where-Object { `$_.Thumbprint -eq `"$thumbprint`" }" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "4. To export as PFX file (optional):" -ForegroundColor Yellow
    Write-Host '   $password = Read-Host "Enter password" -AsSecureString' -ForegroundColor Cyan
    Write-Host "   " -ForegroundColor Gray -NoNewline
    Write-Host 'Export-PfxCertificate -Cert (Get-Item Cert:\CurrentUser\My\' -ForegroundColor Cyan -NoNewline
    Write-Host $thumbprint -ForegroundColor Green -NoNewline
    Write-Host ') -FilePath "cert.pfx" -Password $password' -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Certificate Ready!" -ForegroundColor Green
    Write-Host "The API will use this certificate when running with HTTPS." -ForegroundColor Green
    Write-Host ""

}
catch {
    Write-Host "[ERROR] Error creating certificate:" -ForegroundColor Red
    Write-Host "Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Details: $($_.FullyQualifiedErrorId)" -ForegroundColor Red
    Write-Host "Category: $($_.CategoryInfo.Category)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Make sure you're running PowerShell as Administrator" -ForegroundColor Yellow
    Write-Host "2. Check if Windows certificate store is accessible" -ForegroundColor Yellow
    exit 1
}
