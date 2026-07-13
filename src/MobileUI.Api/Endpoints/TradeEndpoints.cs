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

        group.MapPost("/pause-buying", PauseBuyingAsync)
            .WithName("Pause Buying")
;

        group.MapPost("/resume-buying", ResumeBuyingAsync)
            .WithName("Resume Buying")
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
        if (string.IsNullOrEmpty(request.Ticker))
            return Results.BadRequest(new { error = "Ticker is required" });

        try
        {
            var commandId = await commandManager.CreateSellCommandAsync(request.Ticker);
            var command = await commandManager.GetCommandAsync(commandId);

            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                Message = $"Sell order queued for {request.Ticker}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SellAllAsync(ICommandManager commandManager)
    {
        var commandId = await commandManager.CreateSellAllCommandAsync();
        var command = await commandManager.GetCommandAsync(commandId);

        return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
        {
            Id = commandId,
            Status = command?.Status ?? "pending",
            Message = "Sell all positions queued"
        });
    }

    private static async Task<IResult> PauseBuyingAsync(ICommandManager commandManager)
    {
        try
        {
            var commandId = await commandManager.CreatePauseBuyingCommandAsync();
            var command = await commandManager.GetCommandAsync(commandId);
            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                Message = "Pause buying queued"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ResumeBuyingAsync(ICommandManager commandManager)
    {
        try
        {
            var commandId = await commandManager.CreateResumeBuyingCommandAsync();
            var command = await commandManager.GetCommandAsync(commandId);
            return Results.Accepted($"/api/trades/commands/{commandId}", new CommandResponse
            {
                Id = commandId,
                Status = command?.Status ?? "pending",
                Message = "Resume buying queued"
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
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

        if (command.Status != "pending" && command.Status != "queued_for_open")
            return Results.Conflict(new { error = "Cannot cancel: command already executing or completed" });

        var cancelled = await commandManager.CancelCommandAsync(id);
        if (!cancelled)
            return Results.Conflict(new { error = "Command is being processed" });

        return Results.Ok(new { message = "Command cancelled" });
    }
}
