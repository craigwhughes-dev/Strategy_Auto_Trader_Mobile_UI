using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using StrategyTradingAppUI.Maui.Services;

namespace StrategyTradingAppUI.Maui.ViewModels;

public class SettingsViewModel : BindableObject
{
	private string _apiUrl = "";
	private string _certificateThumbprint = "";
	private string _statusMessage = "";
	private bool _showStatusMessage;
	private const string BaseUrlPrefsKey = "api_base_url";
	private const string ThumbprintPrefsKey = "api_cert_thumbprint";

	public string ApiUrl
	{
		get => _apiUrl;
		set { _apiUrl = value; OnPropertyChanged(); }
	}

	public string CertificateThumbprint
	{
		get => _certificateThumbprint;
		set { _certificateThumbprint = value; OnPropertyChanged(); }
	}

	public string StatusMessage
	{
		get => _statusMessage;
		set { _statusMessage = value; OnPropertyChanged(); }
	}

	public bool ShowStatusMessage
	{
		get => _showStatusMessage;
		set { _showStatusMessage = value; OnPropertyChanged(); }
	}

	public ICommand SaveCommand { get; }
	public ICommand TestConnectionCommand { get; }

	public SettingsViewModel()
	{
		LoadSettings();

		SaveCommand = new Command(OnSaveSettings);
		TestConnectionCommand = new Command(async () => await OnTestConnectionAsync());
	}

	private void LoadSettings()
	{
		ApiUrl = Preferences.Get(BaseUrlPrefsKey, "http://192.168.1.100:5000");
		CertificateThumbprint = Preferences.Get(ThumbprintPrefsKey, "");
	}

	private void OnSaveSettings()
	{
		if (string.IsNullOrWhiteSpace(ApiUrl))
		{
			StatusMessage = "API URL is required";
			ShowStatusMessage = true;
			return;
		}

		Preferences.Set(BaseUrlPrefsKey, ApiUrl.TrimEnd('/'));
		Preferences.Set(ThumbprintPrefsKey, (CertificateThumbprint ?? "").ToUpperInvariant());

		StatusMessage = "Settings saved successfully";
		ShowStatusMessage = true;
	}

	private async Task OnTestConnectionAsync()
	{
		if (string.IsNullOrWhiteSpace(ApiUrl))
		{
			StatusMessage = "Please enter an API URL first";
			ShowStatusMessage = true;
			return;
		}

		try
		{
			StatusMessage = "Testing connection...";
			ShowStatusMessage = true;

			var thumbprint = (CertificateThumbprint ?? "").ToUpperInvariant();
			var handler = new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
				{
					if (certificate == null)
						return false;
					if (string.IsNullOrEmpty(thumbprint))
						return true;
					return certificate.Thumbprint?.ToUpperInvariant() == thumbprint;
				}
			};

			using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
			var response = await client.GetAsync($"{ApiUrl.TrimEnd('/')}/api/health");

			StatusMessage = response.IsSuccessStatusCode
				? "Connection successful!"
				: $"Connection failed: HTTP {(int)response.StatusCode}";
			ShowStatusMessage = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[TestConnection] {ex}");
			var detail = ex.InnerException?.Message ?? ex.Message;
			StatusMessage = TailscaleDetector.IsConnected()
				? $"Connection failed: {detail}"
				: "Tailscale is not connected on this phone. Open the Tailscale app, connect, then try again.";
			ShowStatusMessage = true;
		}
	}
}
