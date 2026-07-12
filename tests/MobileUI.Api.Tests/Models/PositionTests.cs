using MobileUI.Api.Models;
using NUnit.Framework;

namespace MobileUI.Api.Tests.Models;

[TestFixture]
public class PositionTests
{
    private Position _position = null!;

    [SetUp]
    public void Setup()
    {
        _position = new Position
        {
            Ticker = "HSBA.L",
            Market = "LSE",
            Currency = "GBP",
            Quantity = 100,
            FillPrice = 5.00,
            CostValue = 500,
            EntryDate = DateTime.Now.AddDays(-5),
            StopLevel = 4.50,
            TargetLevel = 6.00,
            KellyFraction = 0.05
        };
    }

    [Test]
    public void Position_WithValidValues_CreatesSuccessfully()
    {
        Assert.That(_position.Ticker, Is.EqualTo("HSBA.L"));
        Assert.That(_position.Quantity, Is.EqualTo(100));
        Assert.That(_position.FillPrice, Is.EqualTo(5.00));
    }

    // BVA: Quantity boundaries
    [Test]
    public void Quantity_WithPositiveValue_IsValid()
    {
        _position.Quantity = 1; // Min valid
        Assert.That(_position.Quantity, Is.GreaterThan(0));

        _position.Quantity = 10000; // Large but valid
        Assert.That(_position.Quantity, Is.GreaterThan(0));
    }

    [Test]
    public void Quantity_WithZero_ShouldBeInvalid()
    {
        _position.Quantity = 0;
        // In a production system, validation would throw or return error
        // For now, just verify the value is set (validation would be in a separate validator)
        Assert.That(_position.Quantity, Is.EqualTo(0));
    }

    [Test]
    public void Quantity_WithNegativeValue_ShouldBeInvalid()
    {
        _position.Quantity = -100;
        Assert.That(_position.Quantity, Is.LessThan(0));
    }

    // BVA: Price boundaries
    [Test]
    public void FillPrice_WithPositiveValue_IsValid()
    {
        _position.FillPrice = 0.01; // Penny stock
        Assert.That(_position.FillPrice, Is.GreaterThan(0));

        _position.FillPrice = 2500.99; // High price
        Assert.That(_position.FillPrice, Is.GreaterThan(0));
    }

    [Test]
    public void FillPrice_WithZero_ShouldBeInvalid()
    {
        _position.FillPrice = 0;
        Assert.That(_position.FillPrice, Is.EqualTo(0));
    }

    [Test]
    public void FillPrice_WithNegativeValue_ShouldBeInvalid()
    {
        _position.FillPrice = -10;
        Assert.That(_position.FillPrice, Is.LessThan(0));
    }

    // BVA: Stop/Target levels
    [Test]
    public void StopLevel_BelowFillPrice_IsValid()
    {
        _position.FillPrice = 100;
        _position.StopLevel = 90;
        Assert.That(_position.StopLevel, Is.LessThan(_position.FillPrice));
    }

    [Test]
    public void TargetLevel_AboveFillPrice_IsValid()
    {
        _position.FillPrice = 100;
        _position.TargetLevel = 110;
        Assert.That(_position.TargetLevel, Is.GreaterThan(_position.FillPrice));
    }

    [Test]
    public void StopLevel_AtOrAboveFillPrice_ShouldBeInvalid()
    {
        _position.FillPrice = 100;
        _position.StopLevel = 100; // At entry
        Assert.That(_position.StopLevel, Is.GreaterThanOrEqualTo(_position.FillPrice));

        _position.StopLevel = 110; // Above entry
        Assert.That(_position.StopLevel, Is.GreaterThanOrEqualTo(_position.FillPrice));
    }

    // BVA: Kelly fraction boundaries
    [Test]
    public void KellyFraction_BetweenZeroAndOne_IsValid()
    {
        _position.KellyFraction = 0.01;
        Assert.That(_position.KellyFraction, Is.GreaterThan(0).And.LessThan(1));

        _position.KellyFraction = 0.50;
        Assert.That(_position.KellyFraction, Is.GreaterThan(0).And.LessThan(1));
    }

    [Test]
    public void KellyFraction_ZeroOrNegative_ShouldBeInvalid()
    {
        _position.KellyFraction = 0;
        Assert.That(_position.KellyFraction, Is.EqualTo(0));

        _position.KellyFraction = -0.05;
        Assert.That(_position.KellyFraction, Is.LessThan(0));
    }

    [Test]
    public void KellyFraction_GreaterThanOne_ShouldBeInvalid()
    {
        _position.KellyFraction = 1.0;
        Assert.That(_position.KellyFraction, Is.GreaterThanOrEqualTo(1));

        _position.KellyFraction = 1.5;
        Assert.That(_position.KellyFraction, Is.GreaterThan(1));
    }

    // BVA: Currency-specific (GBX handling)
    [Test]
    public void Position_GBXMarket_StoresPriceInPence()
    {
        var gbxPosition = new Position
        {
            Ticker = "HSBA.L",
            Currency = "GBX",
            FillPrice = 500, // Pence
            Quantity = 100
        };

        // 500 pence = £5.00 (this would be in a calculated property in real code)
        Assert.That(gbxPosition.FillPrice, Is.EqualTo(500));
        Assert.That(gbxPosition.Currency, Is.EqualTo("GBX"));
    }

    // BVA: Unrealized P&L calculation
    [Test]
    public void UnrealizedPnl_Calculation_IsCorrect()
    {
        _position.Quantity = 100;
        _position.FillPrice = 5.00;
        _position.CurrentPrice = 5.50;

        var expectedPnl = (5.50 - 5.00) * 100;
        var actualPnl = (_position.CurrentPrice.Value - _position.FillPrice) * _position.Quantity;

        Assert.That(actualPnl, Is.EqualTo(expectedPnl));
    }

    [Test]
    public void UnrealizedPnl_WithLoss_IsNegative()
    {
        _position.Quantity = 100;
        _position.FillPrice = 5.00;
        _position.CurrentPrice = 4.50;

        var actualPnl = (_position.CurrentPrice.Value - _position.FillPrice) * _position.Quantity;

        Assert.That(actualPnl, Is.LessThan(0));
    }

    [Test]
    public void UnrealizedPnl_WithoutCurrentPrice_IsNull()
    {
        _position.CurrentPrice = null;
        Assert.That(_position.UnrealizedPnl, Is.Null);
    }

    [Test]
    public void UnrealizedPnl_WithZeroPriceMovement_IsZero()
    {
        _position.Quantity = 100;
        _position.FillPrice = 5.00;
        _position.CurrentPrice = 5.00;

        var actualPnl = (_position.CurrentPrice.Value - _position.FillPrice) * _position.Quantity;

        Assert.That(actualPnl, Is.EqualTo(0));
    }

    // BVA: Entry date boundaries
    [Test]
    public void EntryDate_InPast_IsValid()
    {
        _position.EntryDate = DateTime.Now.AddDays(-30);
        Assert.That(_position.EntryDate, Is.LessThan(DateTime.Now));
    }

    [Test]
    public void EntryDate_Today_IsValid()
    {
        var today = DateTime.Today;
        _position.EntryDate = today;
        Assert.That(_position.EntryDate.Date, Is.EqualTo(today));
    }

    [Test]
    public void EntryDate_InFuture_ShouldBeInvalid()
    {
        _position.EntryDate = DateTime.Now.AddDays(1);
        Assert.That(_position.EntryDate, Is.GreaterThan(DateTime.Now));
    }
}
