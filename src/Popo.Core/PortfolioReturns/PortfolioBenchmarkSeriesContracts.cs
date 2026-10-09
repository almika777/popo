namespace Popo.Core.PortfolioReturns;

internal sealed record BenchmarkSeriesCalculation(
    double ProfitPercent,
    double? TwrPercent,
    IReadOnlyList<PortfolioBenchmarkPoint> Points);

internal sealed record LqdtBenchmarkSeriesCalculation(
    DateOnly StartPriceDate,
    DateOnly EndPriceDate,
    BenchmarkSeriesCalculation Series);

internal sealed record IndexedBondBenchmarkSeriesCalculation(
    DateOnly? ComparisonStartDate,
    DateOnly? StartObservationDate,
    DateOnly? EndObservationDate,
    double? PortfolioTwrPercent,
    BenchmarkSeriesCalculation? Series,
    string? UnavailableReason);
