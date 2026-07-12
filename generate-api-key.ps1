<#
.SYNOPSIS
Generates a secure random API key for the Strategy Auto Trader API.

.DESCRIPTION
Creates a cryptographically secure random key suitable for API authentication.
The key is displayed and copied to clipboard for easy configuration.

.PARAMETER Length
Length of the API key in bytes. Default: 32 (256 bits)

.EXAMPLE
.\generate-api-key.ps1 -Length 32

#>
param(
    [int]$Length = 32
)

Write-Host "================================" -ForegroundColor Cyan
Write-Host "API Key Generation Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

try {
    Write-Host "Generating secure random API key..." -ForegroundColor Green
    Write-Host "  Length: $Length bytes (base64 encoded ~$([Math]::Ceiling($Length * 4 / 3)) characters)" -ForegroundColor Gray
    Write-Host ""

    # Generate random bytes
    $bytes = New-Object byte[] $Length
    [System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($bytes)

    # Convert to base64
    $apiKey = [System.Convert]::ToBase64String($bytes)

    Write-Host "[OK] API Key generated successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "API Key:" -ForegroundColor Cyan
    Write-Host "  " -NoNewline
    Write-Host $apiKey -ForegroundColor White -BackgroundColor Black
    Write-Host ""

    # Copy to clipboard
    $apiKey | Set-Clipboard
    Write-Host "[OK] Copied to clipboard" -ForegroundColor Green
    Write-Host ""

    Write-Host "Configuration Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Set as environment variable (persistent, requires admin):" -ForegroundColor Yellow
    Write-Host "   [Environment]::SetEnvironmentVariable('STRATEGY_API_KEY', `"$apiKey`", 'User')" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "2. Or set for current session only:" -ForegroundColor Yellow
    Write-Host '   $env:STRATEGY_API_KEY = "' -ForegroundColor Cyan -NoNewline
    Write-Host $apiKey -ForegroundColor Green -NoNewline
    Write-Host '"' -ForegroundColor Cyan
    Write-Host ""

    Write-Host "3. Verify the key is set:" -ForegroundColor Yellow
    Write-Host '   $env:STRATEGY_API_KEY' -ForegroundColor Cyan
    Write-Host ""

    Write-Host "[WARNING] IMPORTANT: Store this key securely!" -ForegroundColor Yellow
    Write-Host "   - Never commit to version control" -ForegroundColor Yellow
    Write-Host "   - Only share via secure channels" -ForegroundColor Yellow
    Write-Host "   - Consider rotating periodically" -ForegroundColor Yellow
    Write-Host ""

    Write-Host "API Key Ready!" -ForegroundColor Green
    Write-Host "You can now authenticate requests with the X-Api-Key header:" -ForegroundColor Green
    Write-Host "  curl -H 'X-Api-Key: $apiKey' https://api.example.com/api/health" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "[ERROR] Error generating API key:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
