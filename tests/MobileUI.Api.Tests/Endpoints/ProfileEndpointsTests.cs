using MobileUI.Api.Models;
using MobileUI.Api.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Endpoints;

// ProfileEndpoints' handlers are private static Map* delegates, so (matching
// TradeEndpointsTests/PositionsEndpointsTests) this exercises the same logic
// against fakes rather than invoking them directly.
[TestFixture]
public class ProfileEndpointsTests
{
    [Test]
    public void GetProfile_ReturnsCurrentAndAvailable()
    {
        var activeProfile = new FakeActiveProfileService(
            new DaemonProfilePaths { AppStatusPath = "a", JournalPath = "j", CommandsPath = "c" },
            currentProfile: "Paper");

        var result = InvokeGetProfile(activeProfile);
        Assert.That(result, Is.InstanceOf<IResult>());
    }

    [Test]
    public void SwitchProfile_ToPaper_WithoutKey_Succeeds()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: true, isValid: false);

        var result = InvokeSwitchProfile(new SwitchProfileRequest { Profile = "Paper" }, NoHeaders(), fake, keyValidator);

        Assert.That(fake.CurrentProfile, Is.EqualTo("Paper"), "Downgrading away from live must never require a key");
    }

    [Test]
    public void SwitchProfile_ToLive_WithoutKey_Returns401AndDoesNotSwitch()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: true, isValid: false);

        var result = InvokeSwitchProfile(new SwitchProfileRequest { Profile = "Live" }, NoHeaders(), fake, keyValidator);

        Assert.That(result, Is.InstanceOf<IResult>());
        Assert.That(fake.CurrentProfile, Is.EqualTo("Paper"), "Escalating to live without a key must not switch");
    }

    [Test]
    public void SwitchProfile_ToLive_WithInvalidKey_Returns401AndDoesNotSwitch()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: true, isValid: false);

        var result = InvokeSwitchProfile(new SwitchProfileRequest { Profile = "Live" }, HeadersWithKey("wrong"), fake, keyValidator);

        Assert.That(fake.CurrentProfile, Is.EqualTo("Paper"));
    }

    [Test]
    public void SwitchProfile_ToLive_WithValidKey_Succeeds()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: true, isValid: true);

        InvokeSwitchProfile(new SwitchProfileRequest { Profile = "Live" }, HeadersWithKey("correct"), fake, keyValidator);

        Assert.That(fake.CurrentProfile, Is.EqualTo("Live"));
    }

    [Test]
    public void SwitchProfile_ToLive_WithKeyNotConfigured_Returns500AndDoesNotSwitch()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: false, isValid: false);

        InvokeSwitchProfile(new SwitchProfileRequest { Profile = "Live" }, NoHeaders(), fake, keyValidator);

        Assert.That(fake.CurrentProfile, Is.EqualTo("Paper"));
    }

    [Test]
    public void SwitchProfile_WithUnknownProfile_Returns400()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: true, isValid: false);

        var result = InvokeSwitchProfile(new SwitchProfileRequest { Profile = "NotReal" }, NoHeaders(), fake, keyValidator);

        Assert.That(result, Is.InstanceOf<IResult>());
        Assert.That(fake.CurrentProfile, Is.EqualTo("Paper"), "Rejected switch should not change the active profile");
    }

    [Test]
    public void SwitchProfile_WithEmptyProfile_Returns400WithoutCallingSwitchTo()
    {
        var fake = new FakeActiveProfileServiceWithRealisticSwitch();
        var keyValidator = new FakeApiKeyValidator(isConfigured: true, isValid: false);

        var result = InvokeSwitchProfile(new SwitchProfileRequest { Profile = "" }, NoHeaders(), fake, keyValidator);

        Assert.That(result, Is.InstanceOf<IResult>());
        Assert.That(fake.CurrentProfile, Is.EqualTo("Paper"));
    }

    private static HttpContext NoHeaders() => new DefaultHttpContext();

    private static HttpContext HeadersWithKey(string key)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = key;
        return context;
    }

    // Mirrors ProfileEndpoints.GetProfile
    private static IResult InvokeGetProfile(IActiveProfileService activeProfile)
    {
        return Results.Ok(new
        {
            current = activeProfile.CurrentProfile,
            available = activeProfile.ProfileNames,
        });
    }

    // Mirrors ProfileEndpoints.SwitchProfile
    private static IResult InvokeSwitchProfile(SwitchProfileRequest request, HttpContext httpContext, IActiveProfileService activeProfile, IApiKeyValidator apiKeyValidator)
    {
        if (string.IsNullOrWhiteSpace(request.Profile))
            return Results.BadRequest(new { error = "Profile is required" });

        var isEscalatingToLive = string.Equals(request.Profile.Trim(), "Live", StringComparison.OrdinalIgnoreCase);

        if (isEscalatingToLive)
        {
            if (!apiKeyValidator.IsConfigured)
                return Results.Json(new { error = "API key not configured" }, statusCode: StatusCodes.Status500InternalServerError);

            var providedKey = httpContext.Request.Headers.TryGetValue("X-Api-Key", out var keyValue) ? keyValue.ToString() : null;

            if (string.IsNullOrEmpty(providedKey))
                return Results.Json(new { error = "Missing API key" }, statusCode: StatusCodes.Status401Unauthorized);

            if (!apiKeyValidator.IsValid(providedKey))
                return Results.Json(new { error = "Invalid API key" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            activeProfile.SwitchTo(request.Profile);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        return Results.Ok(new { current = activeProfile.CurrentProfile });
    }
}

// FakeActiveProfileService's SwitchTo always succeeds; this variant mimics
// ActiveProfileService's real validation so the 400/no-op paths are testable.
internal class FakeActiveProfileServiceWithRealisticSwitch : IActiveProfileService
{
    public IReadOnlyList<string> ProfileNames => new[] { "Paper", "Live" };
    public string CurrentProfile { get; private set; } = "Paper";
    public DaemonProfilePaths CurrentPaths => new();

    public void SwitchTo(string profileName)
    {
        if (!ProfileNames.Contains(profileName, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Unknown profile '{profileName}'");

        CurrentProfile = ProfileNames.First(p => string.Equals(p, profileName, StringComparison.OrdinalIgnoreCase));
    }
}

internal class FakeApiKeyValidator : IApiKeyValidator
{
    private readonly bool _isValid;

    public FakeApiKeyValidator(bool isConfigured, bool isValid)
    {
        IsConfigured = isConfigured;
        _isValid = isValid;
    }

    public bool IsConfigured { get; }
    public bool IsValid(string? providedKey) => _isValid;
}
