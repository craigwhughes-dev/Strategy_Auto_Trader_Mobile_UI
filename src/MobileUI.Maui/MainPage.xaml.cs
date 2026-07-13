using StrategyTradingAppUI.Maui.Models;
using StrategyTradingAppUI.Maui.ViewModels;

namespace StrategyTradingAppUI.Maui;

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

	private async void OnSellPositionClicked(object? sender, EventArgs e)
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

	private async void OnSellAllClicked(object? sender, EventArgs e)
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

	private async void OnPauseBuyingClicked(object? sender, EventArgs e)
	{
		var confirmPause = await DisplayAlertAsync("Pause Buying", "Pause new buying? Existing sells will still work.", "Yes", "Cancel");
		if (!confirmPause)
			return;

		if (_viewModel.Positions.Count > 0)
		{
			var sellFirst = await DisplayAlertAsync("Open Positions", "Sell all open positions before pausing?", "Yes", "No");
			if (sellFirst)
			{
				try
				{
					var sellResponse = await _viewModel.SubmitSellAllAsync();
					var sellSummary = sellResponse.Status switch
					{
						"filled" => $"Sell all completed: {sellResponse.Message}",
						"pending" or "queued_for_open" => "Sell all order was queued but has not completed yet.",
						"error" => $"Sell all reported a problem: {sellResponse.Message}",
						_ => $"Sell all result: {sellResponse.Message}"
					};
					await DisplayAlertAsync("Sell All Result", $"{sellSummary}\n\nProceeding to pause buying.", "OK");
				}
				catch (Exception ex)
				{
					await DisplayAlertAsync("Sell All Result",
						$"Exception during sell all: {ex.Message}\n\nProceeding to pause buying.", "OK");
				}
				// Sell-all is best-effort per product decision: whatever happened above
				// (full success, partial failure, or total failure), the pause below is
				// always submitted regardless.
			}
		}

		try
		{
			var response = await _viewModel.SubmitPauseBuyingAsync();
			if (response.Status == "filled")
				await DisplayAlertAsync("Buying Paused", "New buy orders are now paused. Sells still work normally.", "OK");
			else if (response.Status is "pending" or "queued_for_open")
				await DisplayAlertAsync("Pause Queued", "Pause command queued; it will take effect shortly.", "OK");
			else
				await DisplayAlertAsync("Error", $"Failed to pause buying: {response.Message}", "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Exception during pause: {ex.Message}", "OK");
		}
	}

	private async void OnResumeBuyingClicked(object? sender, EventArgs e)
	{
		var confirm = await DisplayAlertAsync("Resume Buying", "Resume new buying? The daemon will start placing BUY orders again.", "Yes", "Cancel");
		if (!confirm)
			return;

		try
		{
			var response = await _viewModel.SubmitResumeBuyingAsync();
			if (response.Status == "filled")
				await DisplayAlertAsync("Buying Resumed", "New buy orders are now enabled.", "OK");
			else if (response.Status is "pending" or "queued_for_open")
				await DisplayAlertAsync("Resume Queued", "Resume command queued; it will take effect shortly.", "OK");
			else
				await DisplayAlertAsync("Error", $"Failed to resume buying: {response.Message}", "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Error", $"Exception during resume: {ex.Message}", "OK");
		}
	}

	private async void OnCancelCommandClicked(object? sender, EventArgs e)
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
