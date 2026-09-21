using NUnit.Framework;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class BondPriceCalculatorTests
{
    [Test]
    public void CalculateDirtyPrice_AddsAccruedInterestToCleanPrice()
    {
        var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
            99.5d,
            1_000d,
            "RUB",
            "RUB",
            12.34d);

        Assert.That(dirtyPrice, Is.EqualTo(1_007.34d).Within(0.000001d));
    }

    [Test]
    public void CalculateDirtyPrice_UsesFaceValueAlreadyConvertedToSettlementCurrency()
    {
        var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
            99.5d,
            92_500d,
            1_250d);

        Assert.That(dirtyPrice, Is.EqualTo(93_287.5d).Within(0.000001d));
    }

    [Test]
    public void CalculateDirtyPrice_ReturnsNullWhenAccruedInterestIsMissing()
    {
        var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
            99.5d,
            1_000d,
            "RUB",
            "RUB",
            null);

        Assert.That(dirtyPrice, Is.Null);
    }

    [Test]
    public void CalculateDirtyPrice_ReturnsNullWhenCurrenciesDiffer()
    {
        var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
            99.5d,
            1_000d,
            "USD",
            "RUB",
            12.34d);

        Assert.That(dirtyPrice, Is.Null);
    }

    [TestCase(0d, 1_000d, 10d)]
    [TestCase(99.5d, 0d, 10d)]
    [TestCase(99.5d, 1_000d, -1d)]
    public void CalculateDirtyPrice_ReturnsNullForInvalidInput(
        double cleanPricePercent,
        double faceValue,
        double accruedInterest)
    {
        var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
            cleanPricePercent,
            faceValue,
            "RUB",
            "RUB",
            accruedInterest);

        Assert.That(dirtyPrice, Is.Null);
    }

    [Test]
    public void CalculateDirtyPrice_ReturnsNullForNonFiniteInputAndOverflow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BondPriceCalculator.CalculateDirtyPrice(
                double.NaN, 1_000d, "RUB", "RUB", 10d), Is.Null);
            Assert.That(BondPriceCalculator.CalculateDirtyPrice(
                double.PositiveInfinity, 1_000d, "RUB", "RUB", 10d), Is.Null);
            Assert.That(BondPriceCalculator.CalculateDirtyPrice(
                99.5d, double.PositiveInfinity, "RUB", "RUB", 10d), Is.Null);
            Assert.That(BondPriceCalculator.CalculateDirtyPrice(
                double.MaxValue, double.MaxValue, "RUB", "RUB", 0d), Is.Null);
        });
    }
}
