using MobileUI.Maui.ViewModels;

namespace MobileUI.Maui;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();
		BindingContext = new SettingsViewModel();
	}
}
