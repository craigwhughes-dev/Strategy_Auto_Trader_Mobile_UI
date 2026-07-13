using MobileUI.Maui.Models;
using MobileUI.Maui.ViewModels;

namespace MobileUI.Maui;

public partial class MainPage : ContentPage
{
	private readonly PositionsViewModel _viewModel;

	public MainPage(PositionsViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
		Loaded += async (s, e) => await viewModel.RefreshAsync();
	}

	private async void OnSellPositionClicked(object sender, EventArgs e)
	{
		if (sender is not Button button || button.CommandParameter is not string ticker)
			return;

		if (_viewModel.Positions.FirstOrDefault(p => p.Ticker == ticker) is not Position position)
			return;

		var marketStatus = _viewModel.GetMarketStatus(position.Market);
		var estimatedValue = position.Quantity * (position.CurrentPrice ?? position.FillPrice);
		var marketWarning = marketStatus?.InTradingHours == false
			? "\n\n⚠️ Market is CLOSED - order will be QUEUED until next market open"
			: "";

		var message = $"Sell {position.Quantity:F0} {ticker} ({position.Currency})" +
			$"\nEstimated value: {estimatedValue:F2}" +
			$"{marketWarning}";

		var result = await DisplayAlertAsync("Confirm Sell", message, "Confirm", "Cancel");
		if (!result)
			return;

		try
		{
			var response = await _viewModel.SubmitSellAsync(ticker);
			if (response.Status == "pending" || response.Status == "queued_for_open")
			{
				await DisplayAlertAsync("Order Submitted", $"Sell order for {ticker} has been queued", "OK");
			}
			else if (response.Status == "filled")
			{
				await DisplayAlertAsync("Order Filled", $"Sell order for {ticker} filled at {response.Message}", "OK");
			}
			else
			{
				await DisplayAlertAsync("Error", $"Failed to submit sell order: {response.Message}", "OK");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Exception during sell: {ex.Message}", "OK");
		}
	}

	private async void OnSellAllClicked(object sender, EventArgs e)
	{
		if (_viewModel.Positions.Count == 0)
		{
			await DisplayAlertAsync("No Positions", "No open positions to sell", "OK");
			return;
		}

		var tickers = string.Join(", ", _viewModel.Positions.Select(p => p.Ticker));
		var message = $"Sell all positions:\n{tickers}";

		var result = await DisplayAlertAsync("Confirm Sell All", message, "Confirm", "Cancel");
		if (!result)
			return;

		try
		{
			var response = await _viewModel.SubmitSellAllAsync();
			if (response.Status == "pending" || response.Status == "queued_for_open")
			{
				await DisplayAlertAsync("Order Submitted", "Sell all order has been queued", "OK");
			}
			else if (response.Status == "filled")
			{
				await DisplayAlertAsync("Order Filled", "Sell all order filled", "OK");
			}
			else
			{
				await DisplayAlertAsync("Error", $"Failed to submit sell all order: {response.Message}", "OK");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Exception during sell all: {ex.Message}", "OK");
		}
	}

	private async void OnCancelCommandClicked(object sender, EventArgs e)
	{
		if (sender is not Button button || button.CommandParameter is not string commandId)
			return;

		var result = await DisplayAlertAsync("Cancel Order", "Cancel this pending order?", "Yes", "No");
		if (!result)
			return;

		try
		{
			var cancelResult = await _viewModel.CancelPendingAsync(commandId);
			if (cancelResult == null)
			{
				await DisplayAlertAsync("Success", "Order cancelled", "OK");
			}
			else
			{
				await DisplayAlertAsync("Cancel failed", cancelResult, "OK");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Failed to cancel: {ex.Message}", "OK");
		}
	}
}
