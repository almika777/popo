namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioBenchmarkHistoryUnavailableException(string message) : Exception(message);

public sealed class InvalidPortfolioBenchmarkHistoryException(string message) : Exception(message);

public sealed record PortfolioBenchmarkPoint(DateOnly Date, double IndexedValue);

public sealed record PortfolioIndexBenchmark(
    string SecId,
    string Name,
    DateOnly StartObservationDate,
    DateOnly EndObservationDate,
    decimal StartValue,
    decimal EndValue,
    double PeriodReturn,
    double ExcessReturn,
    IReadOnlyList<PortfolioBenchmarkPoint> Points);

public sealed record PortfolioBenchmarkResult(
    DateOnly From,
    DateOnly To,
    PortfolioReturnResult Portfolio,
    IReadOnlyList<PortfolioBenchmarkPoint> PortfolioPoints,
    IReadOnlyList<PortfolioIndexBenchmark> Indexes);
