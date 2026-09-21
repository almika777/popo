namespace Popo.Core.Portfolio;

public sealed record PortfolioBondKey(string SecId, string Isin, string BoardId);

public sealed record PublishedCouponValue(
    string SecId,
    string Isin,
    string BoardId,
    DateOnly Date,
    double Value,
    string FaceUnit);

public interface IPortfolioCouponProvider
{
    Task<IReadOnlyList<PublishedCouponValue>> GetLatestPublishedAsync(
        IReadOnlyCollection<PortfolioBondKey> bonds,
        DateOnly asOf,
        CancellationToken cancellationToken);
}
