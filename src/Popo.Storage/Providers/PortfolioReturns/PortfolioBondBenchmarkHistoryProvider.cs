using Microsoft.EntityFrameworkCore;
using Popo.Core.PortfolioReturns;

namespace Popo.Storage.Providers.PortfolioReturns;

public sealed class PortfolioBondBenchmarkHistoryProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioBondBenchmarkHistoryProvider
{
    /// <inheritdoc />
    public async Task<PortfolioBondBenchmarkHistory?> GetHistoryAsync(
        string secId,
        string boardId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secId);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        if (from > through)
        {
            throw new ArgumentOutOfRangeException(nameof(from), "Начальная дата не должна быть позже конечной.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var bond = await context.MoexBondSecurities
            .Where(value => value.SecId == secId && value.BoardId == boardId)
            .Select(value => new BondMetadata(value.FaceUnit, value.CurrencyId))
            .SingleOrDefaultAsync(cancellationToken);

        if (bond is null 
            || string.IsNullOrWhiteSpace(bond.FaceUnit) 
            || string.IsNullOrWhiteSpace(bond.CurrencyId))
            return null;
        
        var historyQuery = context.MoexHistoryYieldsEntities
            .Where(value => value.SecId == secId && value.BoardId == boardId);

        var startingPoint = await historyQuery
            .Where(value => value.TradeDate <= from)
            .OrderByDescending(value => value.TradeDate)
            .Select(value => new PortfolioBondBenchmarkPricePoint(
                value.TradeDate,
                value.Close,
                value.LegalClosePrice,
                value.FaceValue,
                value.CouponPercent,
                value.AccInt))
            .FirstOrDefaultAsync(cancellationToken);

        var periodPoints = await historyQuery
            .Where(value => value.TradeDate > from && value.TradeDate <= through)
            .OrderBy(value => value.TradeDate)
            .Select(value => new PortfolioBondBenchmarkPricePoint(
                value.TradeDate,
                value.Close,
                value.LegalClosePrice,
                value.FaceValue,
                value.CouponPercent,
                value.AccInt))
            .ToListAsync(cancellationToken);

        var couponHistoryFrom = startingPoint?.Date ?? from;
        var couponQuery = context.MoexCouponsEntities
            .Where(value => value.SecId == secId)
            .Where(value => value.CouponDate >= couponHistoryFrom && value.StartDate <= through)
            .OrderBy(value => value.CouponDate)
            .Select(value => new
            {
                value.StartDate,
                value.CouponDate,
                value.RecordDate,
                value.Value,
                value.ValueInRub,
                value.FaceUnit
            });

        var couponPeriods = new List<PortfolioBondBenchmarkCouponPeriod>();
        await foreach (var coupon in couponQuery
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken))
        {
            couponPeriods.Add(new PortfolioBondBenchmarkCouponPeriod(
                coupon.StartDate,
                coupon.CouponDate,
                coupon.RecordDate,
                GetRubleCouponAmount(coupon.ValueInRub, coupon.Value, coupon.FaceUnit)));
        }

        if (startingPoint is not null)
        {
            periodPoints.Insert(0, startingPoint);
        }

        return new PortfolioBondBenchmarkHistory(
            secId,
            boardId,
            bond.FaceUnit,
            bond.CurrencyId,
            periodPoints,
            couponPeriods);
    }

    private sealed record BondMetadata(string FaceUnit, string CurrencyId);

    private static double? GetRubleCouponAmount(double? valueInRub, double? value, string faceUnit)
    {
        if (valueInRub is { } rubAmount && double.IsFinite(rubAmount) && rubAmount > 0)
            return rubAmount;

        return IsRubleUnit(faceUnit) && value is { } faceAmount && double.IsFinite(faceAmount) && faceAmount > 0
            ? faceAmount
            : null;
    }

    private static bool IsRubleUnit(string? faceUnit) =>
        string.Equals(faceUnit, "RUB", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(faceUnit, "SUR", StringComparison.OrdinalIgnoreCase);
}
