namespace Popo.Core.Portfolio;

public sealed record PortfolioBondKey(string SecId, string Isin, string BoardId);

public sealed record PublishedCouponValue(
    string SecId,
    string Isin,
    string BoardId,
    DateOnly Date,
    DateOnly PeriodStartDate,
    double? Value,
    string FaceUnit,
    DateOnly? RecordDate = null,
    double? ValueInRub = null);

public interface IPortfolioCouponProvider
{
    Task<IReadOnlyList<PublishedCouponValue>> GetPublishedHistoryAsync(
        IReadOnlyCollection<PortfolioBondKey> bonds,
        DateOnly asOf,
        CancellationToken cancellationToken);
}
