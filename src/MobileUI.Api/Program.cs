using System.Security.Cryptography.X509Certificates;
using System.Linq;
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

static void ConfigureHttps(Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions, string thumbprint)
{
    foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
    {
        var store = new X509Store(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly);
        var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
        if (certs.Count > 0)
        {
            listenOptions.UseHttps(certs[0]);
            store.Close();
            return;
        }
        store.Close();
    }

    Console.WriteLine("Warning: Certificate not found in CurrentUser or LocalMachine store. HTTPS disabled.");
}

static bool IsLocalInterfaceAddress(string ip)
{
    var target = System.Net.IPAddress.Parse(ip);
    return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
        .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
        .Any(addr => addr.Address.Equals(target));
}

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
        serverOptions.ListenAnyIP(5001, listenOptions => ConfigureHttps(listenOptions, certificateThumbprint));
    }
    else
    {
        // Kestrel's Listen() only registers configuration here; the actual socket bind happens later
        // inside app.Run(), so a try/catch around Listen() can never observe a bad-address failure.
        // Check the address is actually assigned to a local interface before registering it.
        if (!string.IsNullOrEmpty(tailscaleIp) && IsLocalInterfaceAddress(tailscaleIp))
        {
            Console.WriteLine($"Binding to Tailscale interface: {tailscaleIp}");
            serverOptions.Listen(System.Net.IPAddress.Parse(tailscaleIp), 5001,
                listenOptions => ConfigureHttps(listenOptions, certificateThumbprint));
        }
        else
        {
            if (!string.IsNullOrEmpty(tailscaleIp))
            {
                Console.WriteLine($"Configured Tailscale IP {tailscaleIp} is not assigned to any local interface. Binding to 0.0.0.0 instead.");
            }
            else
            {
                Console.WriteLine("No Tailscale IP configured. Binding to 0.0.0.0");
            }

            serverOptions.ListenAnyIP(5001, listenOptions => ConfigureHttps(listenOptions, certificateThumbprint));
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
    Console.Error.WriteLine();
    Console.Error.WriteLine(PortConflictHelper.BuildDiagnosticMessage(new HashSet<int> { 5000, 5001 }));
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
    Environment.Exit(1);
}
