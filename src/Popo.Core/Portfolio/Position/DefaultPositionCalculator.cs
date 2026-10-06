namespace Popo.Core.Portfolio.Position;

public sealed class DefaultPositionCalculator : IPositionCalculator
{
    public PositionCalculationResult Calculate(PositionCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.CurrentCleanPrice.HasValue
            || !double.IsFinite(input.CurrentCleanPrice.Value)
            || input.CurrentCleanPrice.Value <= 0)
        {
            return new(null, new PortfolioPositionValuation(null, null, null, null), null);
        }

        var income = PortfolioPositionCalculator.Calculate(
            input.Trades,
            input.AsOf,
            input.CurrentCleanPrice.Value,
            input.CouponValue,
            input.CouponPeriodDays,
            input.IncludeIndexedNominalIncreaseInPnl);
        double? averageBuyPriceAtCurrentFaceValue = income.RemainingQuantity <= 0
            ? null
            : income.RemainingCleanCost / income.RemainingQuantity;
        var valuation = PortfolioPositionCalculator.Calculate(
            input.Position,
            input.CurrentCleanPrice,
            averageBuyPriceAtCurrentFaceValue);

        return new(income, valuation, averageBuyPriceAtCurrentFaceValue);
    }
}
