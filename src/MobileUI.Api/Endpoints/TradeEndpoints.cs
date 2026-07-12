using MobileUI.Api.Models;
using MobileUI.Api.Services;

namespace MobileUI.Api.Endpoints;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/trades")
            .WithName("Trades")
;

        group.MapPost("/sell", SellAsync)
            .WithName("Sell")
;

        group.MapPost("/sell-all", SellAllAsync)
            .WithName("Sell All")
;

        group.MapGet("/commands", GetPendingCommandsAsync)
            .WithName("Get Pending Commands")
;

        group.MapGet("/commands/{id}", GetCommandAsync)
            .WithName("Get Command Status")
;

        group.MapDelete("/commands/{id}", CancelCommandAsync)
            .WithName("Cancel Command")
;
    }

    private static async Task<IResult> SellAsync(SellRequest request, ICommandManager commandManager)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Ticker))
                return Results.BadRequest(new { error = "Ticker is required" });

            var commandId = await commandManager.CreateSellCommandAsync(request.Ticker);
            var command = await commandManager.GetCommandAsync(commandId);

            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                IsQueued = false,
                Message = $"Sell order queued for {request.Ticker}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> SellAllAsync(ICommandManager commandManager)
    {
        try
        {
            var commandId = await commandManager.CreateSellAllCommandAsync();
            var command = await commandManager.GetCommandAsync(commandId);

            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                IsQueued = false,
                Message = "Sell all positions queued"
            });
        }
        catch (Exception)
        {
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> GetPendingCommandsAsync(ICommandManager commandManager)
    {
        var commands = await commandManager.GetPendingCommandsAsync();
        return Results.Ok(commands.Select(c => new
        {
            c.Id,
            c.Action,
            c.Ticker,
            c.Status,
            c.RequestedAtUtc,
            c.ExpiresAtUtc
        }));
    }

    private static async Task<IResult> GetCommandAsync(string id, ICommandManager commandManager)
    {
        var command = await commandManager.GetCommandAsync(id);
        if (command == null)
            return Results.NotFound(new { error = "Command not found" });

        return Results.Ok(new
        {
            command.Id,
            command.Action,
            command.Ticker,
            command.Status,
            command.RequestedAtUtc,
            command.ExpiresAtUtc,
            command.FillPrice,
            command.ErrorMessage
        });
    }

    private static async Task<IResult> CancelCommandAsync(string id, ICommandManager commandManager)
    {
        var command = await commandManager.GetCommandAsync(id);
        if (command == null)
            return Results.NotFound(new { error = "Command not found" });

        if (command.Status != "pending")
            return Results.Conflict(new { error = "Cannot cancel: command already executing or completed" });

        var cancelled = await commandManager.CancelCommandAsync(id);
        if (!cancelled)
            return Results.Conflict(new { error = "Command is being processed" });

        return Results.Ok(new { message = "Command cancelled" });
    }
}
