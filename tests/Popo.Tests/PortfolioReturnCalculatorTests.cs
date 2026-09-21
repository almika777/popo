using NUnit.Framework;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class PortfolioReturnCalculatorTests
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 2, 1);

    private readonly PortfolioReturnCalculator _calculator = new();

    [Test]
    public void NoCashFlows_ReturnsPeriodAndAnnualizedReturn()
    {
        var result = Calculate(100_000, 110_000);

        Assert.That(result.AbsoluteReturn, Is.EqualTo(10_000).Within(0.001));
        Assert.That(result.PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
        Assert.That(result.AnnualizedReturn, Is.EqualTo(Math.Pow(1.1, 365d / 31) - 1).Within(0.000001));
    }

    [Test]
    public void Deposit_IsRemovedFromAbsoluteReturn()
    {
        var result = _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(new DateOnly(2026, 1, 15), 120_000),
                After(To, 220_000)
            ],
            [new PortfolioCashFlowInput(new DateOnly(2026, 1, 16), PortfolioCashFlowType.Deposit, 100_000)]);

        Assert.That(result.AbsoluteReturn, Is.EqualTo(20_000).Within(0.001));
        Assert.That(result.PeriodReturn, Is.EqualTo(0.2).Within(0.000001));
    }

    [Test]
    public void Withdrawal_IsAddedBackToAbsoluteReturn()
    {
        var result = _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(new DateOnly(2026, 1, 15), 110_000),
                After(To, 80_000)
            ],
            [new PortfolioCashFlowInput(new DateOnly(2026, 1, 16), PortfolioCashFlowType.Withdrawal, 30_000)]);

        Assert.That(result.AbsoluteReturn, Is.EqualTo(10_000).Within(0.001));
        Assert.That(result.PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public void Tax_IsAddedToAbsoluteReturnButExcludedFromTimeWeightedReturn()
    {
        var result = _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(new DateOnly(2026, 1, 15), 110_000),
                After(To, 100_000)
            ],
            [new PortfolioCashFlowInput(new DateOnly(2026, 1, 16), PortfolioCashFlowType.Tax, 10_000)]);

        Assert.That(result.Taxes, Is.EqualTo(10_000).Within(0.001));
        Assert.That(result.AbsoluteReturn, Is.EqualTo(10_000).Within(0.001));
        Assert.That(result.PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public void FlowsOutsidePeriod_AreIgnored()
    {
        var result = Calculate(
            100_000,
            110_000,
            new PortfolioCashFlowInput(From, PortfolioCashFlowType.Deposit, 50_000),
            new PortfolioCashFlowInput(new DateOnly(2026, 2, 2), PortfolioCashFlowType.Withdrawal, 50_000));

        Assert.That(result.Deposits, Is.Zero);
        Assert.That(result.Withdrawals, Is.Zero);
        Assert.That(result.PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public void MultipleFlows_AreAggregated()
    {
        var result = _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(new DateOnly(2026, 1, 10), 120_000),
                After(new DateOnly(2026, 1, 20), 145_000),
                After(To, 140_000)
            ],
            [
                new PortfolioCashFlowInput(new DateOnly(2026, 1, 11), PortfolioCashFlowType.Deposit, 20_000),
                new PortfolioCashFlowInput(new DateOnly(2026, 1, 21), PortfolioCashFlowType.Withdrawal, 5_000)
            ]);

        Assert.That(result.Deposits, Is.EqualTo(20_000));
        Assert.That(result.Withdrawals, Is.EqualTo(5_000));
        Assert.That(result.AbsoluteReturn, Is.EqualTo(25_000).Within(0.001));
    }

    [Test]
    public void IntermediateValuations_ProduceTimeWeightedReturnInsteadOfModifiedDietz()
    {
        var result = _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(new DateOnly(2026, 1, 9), 110_000),
                After(To, 210_000)
            ],
            [new PortfolioCashFlowInput(new DateOnly(2026, 1, 10), PortfolioCashFlowType.Deposit, 100_000)]);

        Assert.That(result.PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public void CashFlowWithoutPreviousDayValuation_ReportsPreviousDay()
    {
        var exception = Assert.Throws<MissingPortfolioValuationsException>(() => _calculator.Calculate(
            From,
            To,
            [
                new PortfolioValuationInput(From, 100_000),
                new PortfolioValuationInput(To, 110_000)
            ],
            [new PortfolioCashFlowInput(new DateOnly(2026, 1, 16), PortfolioCashFlowType.Deposit, 10_000)]));

        Assert.That(exception!.RequiredDates, Is.EqualTo(new[] { new DateOnly(2026, 1, 15) }));
    }

    [Test]
    public void CashFlowUsesPreviousDayValuation()
    {
        var result = _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(new DateOnly(2026, 1, 15), 110_000),
                After(To, 210_000)
            ],
            [new PortfolioCashFlowInput(new DateOnly(2026, 1, 16), PortfolioCashFlowType.Deposit, 100_000)]);

        Assert.That(result.PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public void MissingBoundaryValuation_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _calculator.Calculate(
            From,
            To,
            [After(To, 100_000)],
            []));

        Assert.That(exception!.Message, Is.EqualTo("Укажите оценку портфеля на дату 01.01.2026."));
    }

    [Test]
    public void InvalidPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() => _calculator.Calculate(
            From,
            From,
            [After(From, 100_000)],
            []));
    }

    [Test]
    public void NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => Calculate(
            100_000,
            110_000,
            new PortfolioCashFlowInput(new DateOnly(2026, 1, 16), PortfolioCashFlowType.Deposit, -1)));
    }

    [Test]
    public void DuplicateBoundaryValuation_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _calculator.Calculate(
            From,
            To,
            [
                After(From, 100_000),
                After(From, 100_001),
                After(To, 110_000)
            ],
            []));
    }

    private PortfolioReturnResult Calculate(
        double start,
        double end,
        params PortfolioCashFlowInput[] cashFlows) => _calculator.Calculate(
        From,
        To,
        [After(From, start), After(To, end)],
        cashFlows);

    private static PortfolioValuationInput After(DateOnly date, double value) =>
        new(date, value);
}
