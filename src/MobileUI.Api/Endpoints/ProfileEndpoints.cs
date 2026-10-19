using MobileUI.Api.Models;
using MobileUI.Api.Services;

namespace MobileUI.Api.Endpoints;

public static class ProfileEndpoints
{
    public const string LiveProfileName = "Live";

    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile")
            .WithName("Profile");

        group.MapGet("/", GetProfile)
            .WithName("GetProfile");

        // Exempt from the blanket API-key middleware (see
        // ApiKeyAuthenticationMiddleware.ProfileSwitchPath) — the key is
        // only required here when the target is the live/real-money
        // profile, checked below. Downgrading away from live must never be
        // blocked by a missing/wrong key on whatever device the user has in
        // hand.
        group.MapPost("/switch", SwitchProfile)
            .WithName("SwitchProfile");
    }

    private static IResult GetProfile(IActiveProfileService activeProfile)
    {
        return Results.Ok(new
        {
            current = activeProfile.CurrentProfile,
            available = activeProfile.ProfileNames,
        });
    }

    private static IResult SwitchProfile(
        SwitchProfileRequest request,
        HttpContext httpContext,
        IActiveProfileService activeProfile,
        IApiKeyValidator apiKeyValidator,
        ILogger<Program> logger)
    {
        if (string.IsNullOrWhiteSpace(request.Profile))
            return Results.BadRequest(new { error = "Profile is required" });

        var previous = activeProfile.CurrentProfile;
        var isEscalatingToLive = string.Equals(request.Profile.Trim(), LiveProfileName, StringComparison.OrdinalIgnoreCase);

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

        logger.LogWarning("Daemon profile switched from {Previous} to {Current} via API ({Auth})",
            previous, activeProfile.CurrentProfile, isEscalatingToLive ? "key-verified" : "no-key-required");

        return Results.Ok(new { current = activeProfile.CurrentProfile });
    }
}
