using System.Security.Cryptography.X509Certificates;
using MobileUI.Api.Endpoints;
using MobileUI.Api.Middleware;
using MobileUI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IStatusReader, StatusReader>();
builder.Services.AddScoped<IJournalReader, JournalReader>();
builder.Services.AddScoped<ICommandManager, CommandManager>();
builder.Services.AddHttpClient<IPriceFetcher, PriceFetcher>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalNetwork", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var isDevelopment = builder.Environment.IsDevelopment();

    if (isDevelopment)
    {
        serverOptions.ListenAnyIP(5000);
        serverOptions.ListenAnyIP(5001, listenOptions =>
        {
            var certificateThumbprint = "7618F28C90EE396840E9B980773F8A69147E86CC";
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

            if (certs.Count > 0)
            {
                listenOptions.UseHttps(certs[0]);
            }
            else
            {
                Console.WriteLine("Warning: Certificate not found. HTTPS disabled.");
            }

            store.Close();
        });
    }
    else
    {
        serverOptions.ListenAnyIP(5001, listenOptions =>
        {
            var certificateThumbprint = Environment.GetEnvironmentVariable("CERTIFICATE_THUMBPRINT")
                ?? "7618F28C90EE396840E9B980773F8A69147E86CC";
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

            if (certs.Count > 0)
            {
                listenOptions.UseHttps(certs[0]);
            }

            store.Close();
        });
    }
});

var app = builder.Build();

app.UseMiddleware<AuditLoggingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
}

app.UseCors("AllowLocalNetwork");

app.MapPositionsEndpoints();
app.MapTradeEndpoints();

try
{
    app.Run();
}
catch (IOException ex) when (ex.Message.Contains("already in use"))
{
    Console.Error.WriteLine("ERROR: Port 5000/5001 already in use — is another MobileUI.Api instance running?");
    Console.Error.WriteLine($"Details: {ex.Message}");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
    Environment.Exit(1);
}
