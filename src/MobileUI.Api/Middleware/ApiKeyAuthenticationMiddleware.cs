namespace MobileUI.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValue))
            {
                _logger.LogWarning("Missing API key from {SourceIp} for {Method} {Path}", sourceIp, request.Method, request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing API key" });
                return;
            }

            var providedKey = apiKeyValue.ToString();
            var expectedKey = Environment.GetEnvironmentVariable("STRATEGY_API_KEY");

            if (string.IsNullOrEmpty(expectedKey))
            {
                _logger.LogError("STRATEGY_API_KEY environment variable not set");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "API key not configured" });
                return;
            }

            if (!providedKey.Equals(expectedKey))
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
