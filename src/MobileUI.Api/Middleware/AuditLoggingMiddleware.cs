namespace MobileUI.Api.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly string _auditLogPath;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger, string auditLogPath = "")
    {
        _next = next;
        _logger = logger;
        _auditLogPath = auditLogPath;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var timestamp = DateTime.UtcNow.ToString("O");
        var method = request.Method;
        var path = request.Path.ToString();

        try
        {
            await _next(context);

            var statusCode = context.Response.StatusCode;
            var logMessage = $"{timestamp} | {sourceIp} | {method} {path} | Status: {statusCode}";

            _logger.LogInformation(logMessage);

            if (!string.IsNullOrEmpty(_auditLogPath))
            {
                LogToFile(_auditLogPath, logMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request from {SourceIp} {Method} {Path}", sourceIp, method, path);
            throw;
        }
    }

    private static void LogToFile(string path, string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(path, message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {ex}");
        }
    }
}
