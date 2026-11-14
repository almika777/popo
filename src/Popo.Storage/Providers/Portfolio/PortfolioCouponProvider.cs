using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio.Position;

namespace Popo.Storage.Providers.Portfolio;

public sealed class PortfolioCouponProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioCouponProvider
{
    public async Task<IReadOnlyList<OpenPositionCoupon>> GetCouponScheduleAsync(
        IReadOnlyCollection<string> secIds, DateOnly asOf, CancellationToken cancellationToken)
    {
        var ids = secIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
            return [];

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.MoexCouponsEntities.AsNoTracking()
            .Where(coupon => ids.Contains(coupon.SecId) && coupon.StartDate <= asOf)
            .OrderBy(coupon => coupon.SecId)
            .ThenBy(coupon => coupon.CouponDate)
            .Select(coupon => new OpenPositionCoupon(coupon.SecId, coupon.StartDate,
                coupon.CouponDate, coupon.Value, coupon.FaceUnit, coupon.ValueInRub))
            .ToListAsync(cancellationToken);
    }
}
