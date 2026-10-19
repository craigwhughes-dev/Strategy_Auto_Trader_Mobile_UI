using MobileUI.Api.Services;

namespace MobileUI.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly IApiKeyValidator _apiKeyValidator;
    private const string ApiKeyHeaderName = "X-Api-Key";

    // Profile switch does its own conditional key check (only required when
    // escalating TO the live/real-money profile — downgrading away from it
    // is always allowed so a user isn't locked into live trading by a
    // missing/wrong key), so it's exempt from this blanket gate.
    private const string ProfileSwitchPath = "/api/profile/switch";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger, IApiKeyValidator? apiKeyValidator = null)
    {
        _next = next;
        _logger = logger;
        _apiKeyValidator = apiKeyValidator ?? new ApiKeyValidator();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
            request.Method != HttpMethods.Get && request.Method != HttpMethods.Head &&
            !request.Path.StartsWithSegments(ProfileSwitchPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValue))
            {
                _logger.LogWarning("Missing API key from {SourceIp} for {Method} {Path}", sourceIp, request.Method, request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing API key" });
                return;
            }

            if (!_apiKeyValidator.IsConfigured)
            {
                _logger.LogError("STRATEGY_API_KEY environment variable not set");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "API key not configured" });
                return;
            }

            if (!_apiKeyValidator.IsValid(apiKeyValue.ToString()))
            {
                _logger.LogWarning("Invalid API key from {SourceIp} for {Method} {Path}", sourceIp, request.Method, request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
                return;
            }

            _logger.LogInformation("Authorized request from {SourceIp} for {Method} {Path}", sourceIp, request.Method, request.Path);
        }

        await _next(context);
    }
}
