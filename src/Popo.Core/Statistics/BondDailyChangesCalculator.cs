namespace Popo.Core.Statistics;

public sealed class BondDailyChangesCalculator(
    BondDailyPriceChangeCalculator priceChangeCalculator,
    BondDailyTopDeclinesCalculator declinesCalculator,
    BondDailyTopIncreasesCalculator increasesCalculator)
{
    public BondDailyChanges Calculate(
        IReadOnlyCollection<BondDailyPricePoint> previousPrices,
        IReadOnlyCollection<BondDailyPricePoint> currentPrices)
    {
        var changes = priceChangeCalculator.Calculate(previousPrices, currentPrices);
        return new BondDailyChanges(
            declinesCalculator.Calculate(changes),
            increasesCalculator.Calculate(changes));
    }
}
