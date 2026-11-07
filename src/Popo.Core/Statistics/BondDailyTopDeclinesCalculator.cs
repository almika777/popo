namespace Popo.Core.Statistics;

public sealed class BondDailyTopDeclinesCalculator
{
    public IReadOnlyList<BondDailyChange> Calculate(IReadOnlyCollection<BondDailyChange> changes) => changes
        .Where(change => change.ChangePercent < 0)
        .GroupBy(change => change.SecId, StringComparer.Ordinal)
        .Select(group => group.OrderBy(change => change.ChangePercent)
            .ThenBy(change => change.BoardId, StringComparer.Ordinal)
            .First())
        .OrderBy(change => change.ChangePercent)
        .ThenBy(change => change.SecId, StringComparer.Ordinal)
        .ThenBy(change => change.BoardId, StringComparer.Ordinal)
        .Take(5)
        .ToArray();
}
