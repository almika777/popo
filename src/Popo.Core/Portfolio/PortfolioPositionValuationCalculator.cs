namespace Popo.Core.Portfolio;

public static class PortfolioPositionValuationCalculator
{
    public static PortfolioPositionValuation Calculate(
        PortfolioPositionRecord position,
        double? marketPrice)
        => Calculate(position, marketPrice, position.AverageBuyPrice);

    public static PortfolioPositionValuation Calculate(
        PortfolioPositionRecord position,
        double? marketPrice,
        double? averageBuyPriceAtCurrentFaceValue)
    {
        if (!marketPrice.HasValue || !double.IsFinite(marketPrice.Value) || marketPrice.Value <= 0)
        {
            return new PortfolioPositionValuation(null, null, null, null);
        }

        var marketValue = position.Quantity * marketPrice.Value;
        if (!averageBuyPriceAtCurrentFaceValue.HasValue
            || !double.IsFinite(averageBuyPriceAtCurrentFaceValue.Value)
            || averageBuyPriceAtCurrentFaceValue.Value <= 0)
        {
            return new PortfolioPositionValuation(marketPrice.Value, marketValue, null, null);
        }

        var cost = position.Quantity * averageBuyPriceAtCurrentFaceValue.Value;
        var unrealizedPnl = marketValue - cost;
        var unrealizedPnlPercent = cost == 0 ? 0 : unrealizedPnl / cost * 100;

        return new PortfolioPositionValuation(
            marketPrice.Value,
            marketValue,
            unrealizedPnl,
            unrealizedPnlPercent);
    }
}
