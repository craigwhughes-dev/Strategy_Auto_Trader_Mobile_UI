using System.Windows.Input;
using Microsoft.Maui.Controls;
using StrategyTradingAppUI.Maui.Services;

namespace StrategyTradingAppUI.Maui.ViewModels;

public class SettingsViewModel : BindableObject
{
	private string _apiUrl = "";
	private string _apiKey = "";
	private string _certificateThumbprint = "";
	private string _statusMessage = "";
	private bool _showStatusMessage;
	private ApiClient? _apiClient;
	private const string BaseUrlPrefsKey = "api_base_url";
	private const string ThumbprintPrefsKey = "api_cert_thumbprint";
	private const string ApiKeyPrefsKey = "api_key";

	public string ApiUrl
	{
		get => _apiUrl;
		set { _apiUrl = value; OnPropertyChanged(); }
	}

	public string ApiKey
	{
		get => _apiKey;
		set { _apiKey = value; OnPropertyChanged(); }
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
		_apiClient = ServiceHelper.GetService<ApiClient>();

		if (_apiClient == null)
		{
			StatusMessage = "ERROR: API client not initialized";
			ShowStatusMessage = true;
		}

		InitializeAsync();

		SaveCommand = new Command(OnSaveSettings);
		TestConnectionCommand = new Command(OnTestConnection);
	}

	private async void InitializeAsync()
	{
		await LoadSettingsAsync();
	}

	private async Task LoadSettingsAsync()
	{
		ApiUrl = Preferences.Get(BaseUrlPrefsKey, "http://192.168.1.100:5000");
		CertificateThumbprint = Preferences.Get(ThumbprintPrefsKey, "");
		ApiKey = await SecureStorage.GetAsync(ApiKeyPrefsKey) ?? "";
	}

	private async void OnSaveSettings()
	{
		if (_apiClient == null)
		{
			StatusMessage = "API client not initialized";
			ShowStatusMessage = true;
			return;
		}

		if (string.IsNullOrWhiteSpace(ApiUrl) || string.IsNullOrWhiteSpace(ApiKey))
		{
			StatusMessage = "API URL and API Key are required";
			ShowStatusMessage = true;
			return;
		}

		try
		{
			_apiClient.SetBaseUrl(ApiUrl);
			_apiClient.SetCertificateThumbprint(CertificateThumbprint ?? "");
			await _apiClient.SetApiKeyAsync(ApiKey);

			StatusMessage = "Settings saved successfully";
			ShowStatusMessage = true;
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error saving settings: {ex.Message}";
			ShowStatusMessage = true;
		}
	}

	private async void OnTestConnection()
	{
		if (_apiClient == null)
		{
			StatusMessage = "API client not initialized";
			ShowStatusMessage = true;
			return;
		}

		if (string.IsNullOrWhiteSpace(ApiUrl) || string.IsNullOrWhiteSpace(ApiKey))
		{
			StatusMessage = "Please save settings first";
			ShowStatusMessage = true;
			return;
		}

		try
		{
			StatusMessage = "Testing connection...";
			ShowStatusMessage = true;

			_apiClient.SetBaseUrl(ApiUrl);
			_apiClient.SetCertificateThumbprint(CertificateThumbprint);

			await _apiClient.SetApiKeyAsync(ApiKey);
			var positions = await _apiClient.GetPositionsAsync();

			StatusMessage = $"Connection successful! Found {positions.Count} positions.";
			ShowStatusMessage = true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[TestConnection] {ex}");
				var detail = ex.InnerException?.Message ?? ex.Message;
			StatusMessage = $"Connection failed: {detail}";
			ShowStatusMessage = true;
		}
	}
}
