using System.Security.Cryptography;
using System.Text;

namespace MobileUI.Api.Services;

public interface IApiKeyValidator
{
    bool IsConfigured { get; }
    bool IsValid(string? providedKey);
}

/// <summary>
/// Shared X-Api-Key check, used both by the blanket middleware gate on
/// mutating /api/* calls and by endpoints (profile switch) that enforce the
/// key conditionally rather than unconditionally.
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private const string EnvVarName = "STRATEGY_API_KEY";

    public bool IsConfigured => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarName));

    public bool IsValid(string? providedKey)
    {
        var expectedKey = Environment.GetEnvironmentVariable(EnvVarName);
        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(providedKey))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedKey),
            Encoding.UTF8.GetBytes(expectedKey));
    }
}
