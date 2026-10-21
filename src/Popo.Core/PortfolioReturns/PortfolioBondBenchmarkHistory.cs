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
    string BoardId,
    string FaceUnit,
    string CurrencyId,
    IReadOnlyList<PortfolioBondBenchmarkPricePoint> Points,
    IReadOnlyList<PortfolioBondBenchmarkCouponPeriod> CouponPeriods);

public interface IPortfolioBondBenchmarkHistoryProvider
{
    /// <summary>Загружает историю облигации на указанной торговой доске за диапазон дат.</summary>
    /// <param name="secId">Код инструмента MOEX.</param>
    /// <param name="boardId">Идентификатор торговой доски MOEX.</param>
    /// <param name="from">Начало диапазона, включая опорную котировку до этой даты.</param>
    /// <param name="through">Конец диапазона включительно.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>История пары инструмент-доска либо <see langword="null"/>, если такой записи или обязательных данных нет.</returns>
    Task<PortfolioBondBenchmarkHistory?> GetHistoryAsync(
        string secId,
        string boardId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken);
}
