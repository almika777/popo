namespace Popo.Core.PortfolioReturns;

public sealed record PortfolioBondBenchmarkPricePoint(
    DateOnly Date,
    double? ClosePricePercent,
    double? LegalClosePricePercent,
    double FaceValue,
    double CouponPercent,
    double? AccruedInterest = null);

public sealed record PortfolioBondBenchmarkCouponPeriod(
    DateOnly PeriodStartDate,
    DateOnly CouponDate,
    DateOnly? RecordDate,
    double? AmountInRub);

public sealed record PortfolioBondBenchmarkHistory(
    string SecId,
    string Isin,
    string BoardId,
    string FaceUnit,
    string CurrencyId,
    IReadOnlyList<PortfolioBondBenchmarkPricePoint> Points,
    IReadOnlyList<PortfolioBondBenchmarkCouponPeriod> CouponPeriods);

public interface IPortfolioBondBenchmarkHistoryProvider
{
    Task<PortfolioBondBenchmarkHistory?> GetHistoryAsync(
        string isin,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken);
}
