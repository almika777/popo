namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioBenchmarkHistoryUnavailableException(string message) : Exception(message);

public sealed class InvalidPortfolioBenchmarkHistoryException(string message) : Exception(message);

public sealed class MissingPortfolioBenchmarkValuationsException(IReadOnlyList<DateOnly> requiredDates)
    : InvalidOperationException("Для каждой денежной операции нужна оценка портфеля на конец этой даты после операции.")
{
    public IReadOnlyList<DateOnly> RequiredDates { get; } = requiredDates;
}

public sealed record PortfolioBenchmarkPoint(
    DateOnly Date,
    double ReturnPercent,
    double? ProfitPercent = null);

public sealed record PortfolioBenchmarkResult(
    DateOnly From,
    DateOnly To,
    double? PortfolioReturn,
    double LqdtReturn,
    double? ExcessReturn,
    DateOnly LqdtStartObservationDate,
    DateOnly LqdtEndObservationDate,
    IReadOnlyList<PortfolioBenchmarkPoint> PortfolioPoints,
    IReadOnlyList<PortfolioBenchmarkPoint> LqdtPoints,
    double? IndexedBondReturn = null,
    DateOnly? IndexedBondStartObservationDate = null,
    DateOnly? IndexedBondEndObservationDate = null,
    IReadOnlyList<PortfolioBenchmarkPoint>? IndexedBondPoints = null,
    string? IndexedBondUnavailableReason = null,
    DateOnly? IndexedBondComparisonStartDate = null,
    double? PortfolioProfitPercent = null,
    double? LqdtProfitPercent = null,
    double? IndexedBondProfitPercent = null,
    string? PortfolioUnavailableReason = null,
    DateOnly? ComparisonStartDate = null);
