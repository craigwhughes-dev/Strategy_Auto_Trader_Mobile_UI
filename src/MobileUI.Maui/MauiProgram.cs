using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using StrategyTradingAppUI.Maui.ViewModels;

namespace StrategyTradingAppUI.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID
		WebViewHandler.Mapper.AppendToMapping("SslTolerant", (handler, view) =>
		{
			if (handler.PlatformView is Android.Webkit.WebView androidWebView)
			{
				androidWebView.SetWebViewClient(new SslTolerantWebViewClient());
				// Live trading status must always reflect the server, never a stale cached response.
				androidWebView.Settings.CacheMode = Android.Webkit.CacheModes.NoCache;
				androidWebView.ClearCache(true);
			}
		});
#endif

		builder.Services.AddSingleton<SettingsViewModel>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		ServiceHelper.Init(app.Services);
		return app;
	}
}
