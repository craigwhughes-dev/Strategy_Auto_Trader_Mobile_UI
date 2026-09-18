namespace StrategyTradingAppUI.Maui;

public partial class MainPage : ContentPage
{
	private const string BaseUrlPrefsKey = "api_base_url";
	private const string DefaultBaseUrl = "http://localhost:5000";

	public MainPage()
	{
		InitializeComponent();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		var baseUrl = Preferences.Get(BaseUrlPrefsKey, DefaultBaseUrl);
		if (StatusWebView.Source is not UrlWebViewSource currentSource || currentSource.Url != baseUrl)
		{
			StatusWebView.Source = baseUrl;
		}
	}

	private void OnReloadClicked(object? sender, EventArgs e)
	{
		StatusWebView.Reload();
	}
}
