namespace Popo.Core.Statistics;

public sealed class BondDailyTopTurnoversCalculator
{
    public IReadOnlyList<BondDailyTurnover> Calculate(IReadOnlyCollection<BondDailyTurnover> turnovers) => turnovers
        .Where(turnover => !string.IsNullOrWhiteSpace(turnover.SecId)
            && !string.IsNullOrWhiteSpace(turnover.BoardId)
            && double.IsFinite(turnover.TurnoverRub)
            && turnover.TurnoverRub > 0)
        .GroupBy(turnover => turnover.SecId, StringComparer.Ordinal)
        .Select(group => group.OrderByDescending(turnover => turnover.TurnoverRub)
            .ThenBy(turnover => turnover.BoardId, StringComparer.Ordinal)
            .First())
        .OrderByDescending(turnover => turnover.TurnoverRub)
        .ThenBy(turnover => turnover.SecId, StringComparer.Ordinal)
        .ThenBy(turnover => turnover.BoardId, StringComparer.Ordinal)
        .Take(5)
        .ToArray();
}
