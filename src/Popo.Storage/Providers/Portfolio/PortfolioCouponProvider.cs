using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioCouponProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioCouponProvider
{
    public async Task<IReadOnlyList<PublishedCouponValue>> GetCouponScheduleAsync(
        IReadOnlyCollection<PortfolioBondKey> bonds,
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
            .Where(x => securityIds.Contains(x.SecId))
            .Select(x => new PublishedCouponValue(
                x.SecId,
                x.Isin,
                x.BoardId,
                x.CouponDate,
                x.StartDate,
                x.Value,
                x.FaceUnit,
                x.RecordDate,
                x.ValueInRub))
            .ToListAsync(cancellationToken);

        return FilterMatchingBonds(bonds, coupons);
    }

    public async Task<IReadOnlyList<PublishedCouponValue>> GetPublishedHistoryAsync(
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
            .Where(x => securityIds.Contains(x.SecId) && x.CouponDate <= asOf)
            .Select(x => new PublishedCouponValue(
                x.SecId,
                x.Isin,
                x.BoardId,
                x.CouponDate,
                x.StartDate,
                x.Value,
                x.FaceUnit,
                x.RecordDate,
                x.ValueInRub))
            .ToListAsync(cancellationToken);

        return FilterMatchingBonds(bonds, coupons);
    }

    private static IReadOnlyList<PublishedCouponValue> FilterMatchingBonds(
        IReadOnlyCollection<PortfolioBondKey> bonds,
        IEnumerable<PublishedCouponValue> coupons) => coupons
            .Where(coupon => bonds.Any(bond => Matches(bond, coupon)))
            .OrderBy(x => x.SecId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.BoardId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Date)
            .ToArray();

    private static bool Matches(PortfolioBondKey bond, PublishedCouponValue coupon) =>
        string.Equals(bond.SecId, coupon.SecId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(bond.Isin, coupon.Isin, StringComparison.OrdinalIgnoreCase);

}
