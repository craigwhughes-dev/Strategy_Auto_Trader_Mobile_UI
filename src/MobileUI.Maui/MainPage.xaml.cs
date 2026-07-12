using MobileUI.Maui.ViewModels;

namespace MobileUI.Maui;

public partial class MainPage : ContentPage
{
	public MainPage(PositionsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		Loaded += async (s, e) => await viewModel.RefreshAsync();
	}
}
