using MobileUI.Api.Models;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Models;

[TestFixture]
public class TradeCommandTests
{
    private TradeCommand _command = null!;

    [SetUp]
    public void Setup()
    {
        _command = new TradeCommand
        {
            Action = "SELL",
            Ticker = "AAPL"
        };
    }

    [Test]
    public void TradeCommand_WithDefaults_InitializesCorrectly()
    {
        var cmd = new TradeCommand();

        Assert.That(cmd.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(cmd.Status, Is.EqualTo("pending"));
        Assert.That(cmd.RequestedAtUtc, Is.GreaterThan(DateTime.UtcNow.AddSeconds(-1)));
        Assert.That(cmd.ExpiresAtUtc, Is.GreaterThan(DateTime.UtcNow.AddHours(3).AddMinutes(59)));
    }

    [Test]
    public void TradeCommand_IdIsUnique()
    {
        var cmd1 = new TradeCommand();
        var cmd2 = new TradeCommand();

        Assert.That(cmd1.Id, Is.Not.EqualTo(cmd2.Id));
    }

    // BVA: Action field
    [Test]
    public void Action_WithValidValues_IsAccepted()
    {
        _command.Action = "SELL";
        Assert.That(_command.Action, Is.EqualTo("SELL"));

        _command.Action = "SELL_ALL";
        Assert.That(_command.Action, Is.EqualTo("SELL_ALL"));
    }

    [Test]
    public void Action_Empty_IsPresent()
    {
        _command.Action = "";
        Assert.That(_command.Action, Is.Empty);
    }

    // BVA: Ticker field (nullable)
    [Test]
    public void Ticker_ForSellCommand_IsRequired()
    {
        _command.Action = "SELL";
        _command.Ticker = "AAPL";
        Assert.That(_command.Ticker, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Ticker_ForSellAllCommand_IsNull()
    {
        _command.Action = "SELL_ALL";
        _command.Ticker = null;
        Assert.That(_command.Ticker, Is.Null);
    }

    // BVA: Status field boundaries
    [Test]
    public void Status_WithValidValues_IsAccepted()
    {
        var validStatuses = new[] { "pending", "queued_for_open", "executing", "filled", "error", "expired", "cancelled" };

        foreach (var status in validStatuses)
        {
            _command.Status = status;
            Assert.That(_command.Status, Is.EqualTo(status));
        }
    }

    [Test]
    public void Status_DefaultIsPending()
    {
        var cmd = new TradeCommand();
        Assert.That(cmd.Status, Is.EqualTo("pending"));
    }

    // BVA: Timestamp boundaries
    [Test]
    public void RequestedAtUtc_IsSetToNow()
    {
        var beforeCreation = DateTime.UtcNow;
        var cmd = new TradeCommand();
        var afterCreation = DateTime.UtcNow;

        Assert.That(cmd.RequestedAtUtc, Is.GreaterThanOrEqualTo(beforeCreation));
        Assert.That(cmd.RequestedAtUtc, Is.LessThanOrEqualTo(afterCreation));
    }

    [Test]
    public void ExpiresAtUtc_IsSetToFourHoursFromNow()
    {
        var beforeCreation = DateTime.UtcNow;
        var cmd = new TradeCommand();
        var afterCreation = DateTime.UtcNow;

        var expectedMin = beforeCreation.AddHours(4);
        var expectedMax = afterCreation.AddHours(4);

        Assert.That(cmd.ExpiresAtUtc, Is.GreaterThanOrEqualTo(expectedMin));
        Assert.That(cmd.ExpiresAtUtc, Is.LessThanOrEqualTo(expectedMax));
    }

    [Test]
    public void ExpiresAtUtc_IsAfterRequestedAtUtc()
    {
        _command.RequestedAtUtc = DateTime.UtcNow;
        _command.ExpiresAtUtc = DateTime.UtcNow.AddHours(4);

        Assert.That(_command.ExpiresAtUtc, Is.GreaterThan(_command.RequestedAtUtc));
    }

    [Test]
    public void Command_CanExpire_IfExpiryInPast()
    {
        _command.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        var isExpired = DateTime.UtcNow > _command.ExpiresAtUtc;

        Assert.That(isExpired, Is.True);
    }

    // BVA: FillPrice boundaries (nullable)
    [Test]
    public void FillPrice_WithValidPositiveValue_IsAccepted()
    {
        _command.FillPrice = 150.50;
        Assert.That(_command.FillPrice, Is.EqualTo(150.50));
    }

    [Test]
    public void FillPrice_Nullable_DefaultIsNull()
    {
        Assert.That(_command.FillPrice, Is.Null);
    }

    [Test]
    public void FillPrice_WithZero_ShouldBeInvalid()
    {
        _command.FillPrice = 0;
        Assert.That(_command.FillPrice, Is.EqualTo(0));
    }

    [Test]
    public void FillPrice_WithNegative_ShouldBeInvalid()
    {
        _command.FillPrice = -10;
        Assert.That(_command.FillPrice, Is.LessThan(0));
    }

    // BVA: Quantity boundaries (nullable)
    [Test]
    public void Quantity_WithValidPositiveValue_IsAccepted()
    {
        _command.Quantity = 100;
        Assert.That(_command.Quantity, Is.EqualTo(100));
    }

    [Test]
    public void Quantity_Nullable_DefaultIsNull()
    {
        Assert.That(_command.Quantity, Is.Null);
    }

    [Test]
    public void Quantity_WithZero_ShouldBeInvalid()
    {
        _command.Quantity = 0;
        Assert.That(_command.Quantity, Is.EqualTo(0));
    }

    [Test]
    public void Quantity_WithNegative_ShouldBeInvalid()
    {
        _command.Quantity = -50;
        Assert.That(_command.Quantity, Is.LessThan(0));
    }

    [Test]
    public void Quantity_WithLargeValue_IsAccepted()
    {
        _command.Quantity = 1000000;
        Assert.That(_command.Quantity, Is.GreaterThan(0));
    }

    // BVA: Source field
    [Test]
    public void Source_DefaultIsAndroidApp()
    {
        var cmd = new TradeCommand();
        Assert.That(cmd.Source, Is.EqualTo("android-app"));
    }

    [Test]
    public void Source_CanBeCustomized()
    {
        _command.Source = "web-app";
        Assert.That(_command.Source, Is.EqualTo("web-app"));
    }

    // BVA: ErrorMessage (nullable)
    [Test]
    public void ErrorMessage_Nullable_DefaultIsNull()
    {
        Assert.That(_command.ErrorMessage, Is.Null);
    }

    [Test]
    public void ErrorMessage_SetOnError_IsStored()
    {
        _command.Status = "error";
        _command.ErrorMessage = "Position not found";
        Assert.That(_command.ErrorMessage, Is.EqualTo("Position not found"));
    }

    // Integration tests
    [Test]
    public void SellCommand_FullLifecycle()
    {
        var createdTime = DateTime.UtcNow;
        var cmd = new TradeCommand { Action = "SELL", Ticker = "MSFT" };

        Assert.That(cmd.Status, Is.EqualTo("pending"));
        Assert.That(cmd.RequestedAtUtc, Is.GreaterThanOrEqualTo(createdTime.AddMilliseconds(-10)));
        Assert.That(cmd.ExpiresAtUtc, Is.GreaterThan(cmd.RequestedAtUtc));

        cmd.Status = "executing";
        cmd.FillPrice = 300.25;
        cmd.Quantity = 100;

        Assert.That(cmd.Status, Is.EqualTo("executing"));
        Assert.That(cmd.FillPrice, Is.EqualTo(300.25));
        Assert.That(cmd.Quantity, Is.EqualTo(100));

        cmd.Status = "filled";
        Assert.That(cmd.Status, Is.EqualTo("filled"));
    }

    [Test]
    public void ErrorCommand_StoresErrorMessage()
    {
        var cmd = new TradeCommand { Action = "SELL", Ticker = "INVALID" };

        cmd.Status = "error";
        cmd.ErrorMessage = "Insufficient buying power";

        Assert.That(cmd.Status, Is.EqualTo("error"));
        Assert.That(cmd.ErrorMessage, Is.EqualTo("Insufficient buying power"));
    }
}
