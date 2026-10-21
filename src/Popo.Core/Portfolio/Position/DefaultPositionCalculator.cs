namespace Popo.Core.Portfolio.Position;

public sealed class DefaultPositionCalculator : IPositionCalculator
{
    public PositionCalculationResult Calculate(PositionCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var openLots = PositionOpenLotCalculator.Calculate(input.Trades, input.AsOf);
        if (openLots.RemainingQuantity <= 0)
            return new(null, new PortfolioPositionValuation(null, null, null, null), null);

        var averageBuyPriceAtCurrentFaceValue = openLots.RemainingCleanCost / openLots.RemainingQuantity;
        var averageBuyPrice = openLots.ActualRemainingCleanCost / openLots.RemainingQuantity;

        if (!input.CurrentCleanPrice.HasValue
            || !double.IsFinite(input.CurrentCleanPrice.Value)
            || input.CurrentCleanPrice.Value <= 0)
        {
            return new(
                null,
                new PortfolioPositionValuation(null, null, null, null),
                averageBuyPriceAtCurrentFaceValue,
                averageBuyPrice);
        }

        var income = PortfolioPositionCalculator.Calculate(
            input.Trades,
            input.AsOf,
            input.CurrentCleanPrice.Value,
            input.CouponValue,
            input.CouponPeriodDays,
            input.IncludeIndexedNominalIncreaseInPnl);
        var valuation = PortfolioPositionCalculator.Calculate(
            input.Position,
            input.CurrentCleanPrice,
            averageBuyPriceAtCurrentFaceValue);

        return new(income, valuation, averageBuyPriceAtCurrentFaceValue, averageBuyPrice);
    }
}
