using StrategyTradingAppUI.Maui.ViewModels;

namespace StrategyTradingAppUI.Maui;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();
		BindingContext = new SettingsViewModel();
	}
}
