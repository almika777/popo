using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioCouponProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioCouponProvider
{
    public async Task<IReadOnlyList<PublishedCouponValue>> GetLatestPublishedAsync(
        IReadOnlyCollection<PortfolioBondKey> bonds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        if (bonds.Count == 0)
            return [];

        var securityIds = bonds
            .Select(x => x.SecId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var coupons = await dbContext.MoexCouponsEntities
            .AsNoTracking()
            .Where(x => securityIds.Contains(x.SecId) && x.CouponDate <= asOf && x.Value.HasValue)
            .Select(x => new PublishedCouponValue(
                x.SecId,
                x.Isin,
                x.BoardId,
                x.CouponDate,
                x.Value!.Value,
                x.FaceUnit))
            .ToListAsync(cancellationToken);

        return coupons
            .Where(coupon => bonds.Any(bond => Matches(bond, coupon)))
            .GroupBy(x => $"{x.SecId}\u001f{x.Isin}\u001f{x.BoardId}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(coupon => coupon.Date).First())
            .ToArray();
    }

    private static bool Matches(PortfolioBondKey bond, PublishedCouponValue coupon) =>
        string.Equals(bond.SecId, coupon.SecId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(bond.Isin, coupon.Isin, StringComparison.OrdinalIgnoreCase)
        && string.Equals(bond.BoardId, coupon.BoardId, StringComparison.OrdinalIgnoreCase);

}
