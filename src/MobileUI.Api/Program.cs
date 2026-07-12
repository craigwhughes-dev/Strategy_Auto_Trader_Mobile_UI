using System.Security.Cryptography.X509Certificates;
using MobileUI.Api.Endpoints;
using MobileUI.Api.Middleware;
using MobileUI.Api.Services;
using static MobileUI.Api.Middleware.AuditLoggingMiddleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection("Audit"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IStatusReader, StatusReader>();
builder.Services.AddSingleton<IJournalReader, JournalReader>();
builder.Services.AddSingleton<ICommandManager, CommandManager>();
builder.Services.AddSingleton<IPriceFetcher>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<PriceFetcher>>();
    var httpClient = httpClientFactory.CreateClient();
    return new PriceFetcher(httpClient, logger);
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var isDevelopment = builder.Environment.IsDevelopment();
    var certificateThumbprint = builder.Configuration["Security:CertificateThumbprint"]
        ?? Environment.GetEnvironmentVariable("CERTIFICATE_THUMBPRINT")
        ?? throw new InvalidOperationException("Certificate thumbprint not configured");

    var tailscaleIp = builder.Configuration["Security:TailscaleInterfaceIp"]
        ?? Environment.GetEnvironmentVariable("TAILSCALE_INTERFACE_IP");

    if (isDevelopment)
    {
        serverOptions.ListenAnyIP(5000);
        serverOptions.ListenAnyIP(5001, listenOptions =>
        {
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
        if (!string.IsNullOrEmpty(tailscaleIp))
        {
            Console.WriteLine($"Binding to Tailscale interface: {tailscaleIp}");
            try
            {
                serverOptions.Listen(System.Net.IPAddress.Parse(tailscaleIp), 5001, listenOptions =>
                {
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
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to bind to Tailscale IP {tailscaleIp}: {ex.Message}");
                Console.WriteLine("Falling back to 0.0.0.0");
                serverOptions.ListenAnyIP(5001, listenOptions =>
                {
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
        }
        else
        {
            Console.WriteLine("No Tailscale IP configured. Binding to 0.0.0.0");
            serverOptions.ListenAnyIP(5001, listenOptions =>
            {
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
    }
});

var app = builder.Build();

app.UseMiddleware<AuditLoggingMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.MapPositionsEndpoints();

var tradeCommandsEnabled = app.Configuration.GetValue<bool>("Features:TradeCommands");
if (tradeCommandsEnabled)
{
    app.MapTradeEndpoints();
}

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
