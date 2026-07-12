using Microsoft.Extensions.Logging;
using MobileUI.Maui.Services;
using MobileUI.Maui.ViewModels;

namespace MobileUI.Maui;

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

		builder.Services.AddSingleton<ApiClient>();
		builder.Services.AddSingleton<IApiClient>(sp => sp.GetRequiredService<ApiClient>());
		builder.Services.AddSingleton<PositionsViewModel>();
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
