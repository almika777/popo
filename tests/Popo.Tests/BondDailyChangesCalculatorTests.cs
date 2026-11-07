using NUnit.Framework;
using Popo.Core.Statistics;

namespace Popo.Tests;

public sealed class BondDailyChangesCalculatorTests
{
    private readonly BondDailyChangesCalculator _calculator = new(new(), new(), new());

    [Test]
    public void Calculate_RanksTopFiveDeclinesAndIncreasesByPercent()
    {
        var basePrices = Enumerable.Range(1, 12)
            .Select(index => Price($"BOND{index}", "TQCB", 100))
            .ToArray();
        var currentPrices = new[]
        {
            Price("BOND1", "TQCB", 90),
            Price("BOND2", "TQCB", 80),
            Price("BOND3", "TQCB", 70),
            Price("BOND4", "TQCB", 60),
            Price("BOND5", "TQCB", 50),
            Price("BOND6", "TQCB", 40),
            Price("BOND7", "TQCB", 110),
            Price("BOND8", "TQCB", 120),
            Price("BOND9", "TQCB", 130),
            Price("BOND10", "TQCB", 140),
            Price("BOND11", "TQCB", 150),
            Price("BOND12", "TQCB", 160)
        };

        var result = _calculator.Calculate(basePrices, currentPrices);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines.Select(item => item.SecId), Is.EqualTo(new[] { "BOND6", "BOND5", "BOND4", "BOND3", "BOND2" }));
            Assert.That(result.Increases.Select(item => item.SecId), Is.EqualTo(new[] { "BOND12", "BOND11", "BOND10", "BOND9", "BOND8" }));
            Assert.That(result.Declines, Has.All.Matches<BondDailyChange>(item => item.ChangePercent < 0));
            Assert.That(result.Increases, Has.All.Matches<BondDailyChange>(item => item.ChangePercent > 0));
        });
    }

    [Test]
    public void Calculate_MatchesBySecIdAndBoardAndReturnsPointAndPercentChanges()
    {
        var result = _calculator.Calculate(
            [Price("BOND1", "TQCB", 100), Price("BOND1", "CETS", 100), Price("BOND2", "TQCB", 100)],
            [Price("BOND1", "TQCB", 95), Price("BOND1", "CETS", 80), Price("BOND1", "TQOB", 50), Price("BOND2", "CETS", 80), Price("BOND2", "TQCB", 105)]);

        var decline = result.Declines.Single();
        var increase = result.Increases.Single();
        Assert.Multiple(() =>
        {
            Assert.That(decline.SecId, Is.EqualTo("BOND1"));
            Assert.That(decline.BoardId, Is.EqualTo("CETS"));
            Assert.That(decline.PreviousPrice, Is.EqualTo(100));
            Assert.That(decline.CurrentPrice, Is.EqualTo(80));
            Assert.That(decline.ChangePoints, Is.EqualTo(-20));
            Assert.That(decline.ChangePercent, Is.EqualTo(-20));
            Assert.That(increase.SecId, Is.EqualTo("BOND2"));
            Assert.That(increase.PreviousPrice, Is.EqualTo(100));
            Assert.That(increase.CurrentPrice, Is.EqualTo(105));
            Assert.That(increase.ChangePoints, Is.EqualTo(5));
            Assert.That(increase.ChangePercent, Is.EqualTo(5));
        });
    }

    [Test]
    public void Calculate_IgnoresInvalidPricesAndBreaksTiesBySecId()
    {
        var result = _calculator.Calculate(
            [Price("BOND-C", "TQCB", 100), Price("BOND-A", "TQCB", 100), Price("BOND-Z", "TQCB", 0)],
            [Price("BOND-C", "TQCB", 90), Price("BOND-A", "TQCB", 90), Price("BOND-Z", "TQCB", 80),
             Price("BOND-NAN", "TQCB", double.NaN), Price("BOND-INF", "TQCB", double.PositiveInfinity)]);

        Assert.That(result.Declines.Select(item => item.SecId), Is.EqualTo(new[] { "BOND-A", "BOND-C" }));
    }

    [Test]
    public void TopDeclinesCalculator_ReturnsFiveLargestDeclinesAndKeepsWorstBoardPerBond()
    {
        var calculator = new BondDailyTopDeclinesCalculator();
        BondDailyChange[] changes =
        [
            Change("BOND-A", "TQCB", -12),
            Change("BOND-A", "CETS", -5),
            Change("BOND-B", "TQCB", -10),
            Change("BOND-C", "TQCB", -9),
            Change("BOND-D", "TQCB", -8),
            Change("BOND-E", "TQCB", -7),
            Change("BOND-F", "TQCB", -6),
            Change("BOND-UP", "TQCB", 10)
        ];

        var result = calculator.Calculate(changes);

        Assert.That(result.Select(item => item.SecId), Is.EqualTo(new[] { "BOND-A", "BOND-B", "BOND-C", "BOND-D", "BOND-E" }));
        Assert.That(result[0].BoardId, Is.EqualTo("TQCB"));
    }

    [Test]
    public void TopIncreasesCalculator_ReturnsFiveLargestIncreasesAndKeepsBestBoardPerBond()
    {
        var calculator = new BondDailyTopIncreasesCalculator();
        BondDailyChange[] changes =
        [
            Change("BOND-A", "TQCB", 12),
            Change("BOND-A", "CETS", 5),
            Change("BOND-B", "TQCB", 10),
            Change("BOND-C", "TQCB", 9),
            Change("BOND-D", "TQCB", 8),
            Change("BOND-E", "TQCB", 7),
            Change("BOND-F", "TQCB", 6),
            Change("BOND-DOWN", "TQCB", -10)
        ];

        var result = calculator.Calculate(changes);

        Assert.That(result.Select(item => item.SecId), Is.EqualTo(new[] { "BOND-A", "BOND-B", "BOND-C", "BOND-D", "BOND-E" }));
        Assert.That(result[0].BoardId, Is.EqualTo("TQCB"));
    }

    [Test]
    public void TopTurnoversCalculator_ReturnsFiveLargestTurnoversAndKeepsOneBoardPerBond()
    {
        var calculator = new BondDailyTopTurnoversCalculator();
        BondDailyTurnover[] turnovers =
        [
            Turnover("BOND-A", "TQCB", 12_000_000),
            Turnover("BOND-A", "CETS", 5_000_000),
            Turnover("BOND-B", "TQCB", 10_000_000),
            Turnover("BOND-C", "TQCB", 9_000_000),
            Turnover("BOND-D", "TQCB", 8_000_000),
            Turnover("BOND-E", "TQCB", 7_000_000),
            Turnover("BOND-F", "TQCB", 6_000_000),
            Turnover("BOND-INVALID", "TQCB", double.NaN),
            Turnover("BOND-ZERO", "TQCB", 0)
        ];

        var result = calculator.Calculate(turnovers);

        Assert.That(result.Select(item => item.SecId), Is.EqualTo(new[] { "BOND-A", "BOND-B", "BOND-C", "BOND-D", "BOND-E" }));
        Assert.That(result[0].BoardId, Is.EqualTo("TQCB"));
    }

    private static BondDailyPricePoint Price(string secId, string boardId, double price) =>
        new(secId, boardId, secId, price);

    private static BondDailyChange Change(string secId, string boardId, double percent) =>
        new(secId, boardId, secId, 100, 100 + percent, percent, percent);

    private static BondDailyTurnover Turnover(string secId, string boardId, double turnoverRub) =>
        new(secId, boardId, secId, turnoverRub);

}
