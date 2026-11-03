namespace Popo.Core.Statistics;

public sealed class BondDailyPriceChangeCalculator
{
    public IReadOnlyList<BondDailyChange> Calculate(
        IReadOnlyCollection<BondDailyPricePoint> previousPrices,
        IReadOnlyCollection<BondDailyPricePoint> currentPrices)
    {
        var previousByBoard = previousPrices
            .Where(IsValid)
            .GroupBy(price => (price.SecId, price.BoardId))
            .ToDictionary(group => group.Key, group => group.First());

        return currentPrices
            .Where(IsValid)
            .Select(current => (Current: current, Previous: previousByBoard.GetValueOrDefault((current.SecId, current.BoardId))))
            .Where(pair => pair.Previous is not null)
            .Select(pair => CreateChange(pair.Current, pair.Previous!))
            .Where(change => change.ChangePercent != 0 && double.IsFinite(change.ChangePercent))
            .ToArray();
    }

    private static bool IsValid(BondDailyPricePoint price) =>
        !string.IsNullOrWhiteSpace(price.SecId)
        && !string.IsNullOrWhiteSpace(price.BoardId)
        && double.IsFinite(price.Price)
        && price.Price > 0;

    private static BondDailyChange CreateChange(BondDailyPricePoint current, BondDailyPricePoint previous)
    {
        var changePoints = current.Price - previous.Price;
        var changePercent = changePoints / previous.Price * 100;
        return new BondDailyChange(
            current.SecId,
            current.BoardId,
            string.IsNullOrWhiteSpace(previous.ShortName) ? current.ShortName : previous.ShortName,
            previous.Price,
            current.Price,
            changePoints,
            changePercent,
            current.EmitterId);
    }
}
