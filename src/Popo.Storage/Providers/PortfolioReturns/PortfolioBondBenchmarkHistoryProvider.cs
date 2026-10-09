using Microsoft.EntityFrameworkCore;
using Popo.Core.PortfolioReturns;

namespace Popo.Storage.Providers.PortfolioReturns;

public sealed class PortfolioBondBenchmarkHistoryProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioBondBenchmarkHistoryProvider
{
    public async Task<PortfolioBondBenchmarkHistory?> GetHistoryAsync(
        string isin,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isin);
        if (from > through)
        {
            throw new ArgumentOutOfRangeException(nameof(from), "Начальная дата не должна быть позже конечной.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var bond = await context.MoexBonds
            .AsNoTracking()
            .Where(value => value.Isin == isin)
            .Select(value => new { value.SecId, value.Isin })
            .SingleOrDefaultAsync(cancellationToken);
        if (bond is null)
        {
            return null;
        }

        var secId = bond.SecId;
        var boardId = await context.ActiveSecEntities
            .AsNoTracking()
            .Where(security => security.SecId == secId)
            .Select(security => security.PrimaryBoardId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(boardId))
        {
            return null;
        }

        var securityMetadata = await context.MoexBondSecurities
            .AsNoTracking()
            .Where(value => value.SecId == secId && value.BoardId == boardId)
            .Select(value => new { value.FaceUnit, value.CurrencyId })
            .SingleOrDefaultAsync(cancellationToken);
        if (securityMetadata is null)
        {
            return null;
        }

        var historyQuery = context.MoexHistoryYieldsEntities
            .AsNoTracking()
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
        var couponRows = await context.MoexCouponsEntities
            .AsNoTracking()
            .Where(value => value.SecId == secId && value.Isin == bond.Isin)
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
            })
            .ToListAsync(cancellationToken);

        var couponPeriods = couponRows
            .Select(value => new PortfolioBondBenchmarkCouponPeriod(
                value.StartDate,
                value.CouponDate,
                value.RecordDate,
                GetRubleCouponAmount(value.ValueInRub, value.Value, value.FaceUnit)))
            .ToArray();

        if (startingPoint is not null)
        {
            periodPoints.Insert(0, startingPoint);
        }

        return new PortfolioBondBenchmarkHistory(
            secId,
            bond.Isin,
            boardId,
            securityMetadata.FaceUnit,
            securityMetadata.CurrencyId ?? string.Empty,
            periodPoints,
            couponPeriods);
    }

    private static double? GetRubleCouponAmount(double? valueInRub, double? value, string faceUnit)
    {
        if (valueInRub is { } rubAmount && double.IsFinite(rubAmount) && rubAmount > 0)
        {
            return rubAmount;
        }

        return IsRubleUnit(faceUnit) && value is { } faceAmount && double.IsFinite(faceAmount) && faceAmount > 0
            ? faceAmount
            : null;
    }

    private static bool IsRubleUnit(string? faceUnit) =>
        string.Equals(faceUnit, "RUB", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(faceUnit, "SUR", StringComparison.OrdinalIgnoreCase);
}
