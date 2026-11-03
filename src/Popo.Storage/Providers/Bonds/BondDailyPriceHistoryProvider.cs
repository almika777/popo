using Microsoft.EntityFrameworkCore;
using Popo.Core.Statistics;

namespace Popo.Storage.Providers.Bonds;

public sealed class BondDailyPriceHistoryProvider(IDbContextFactory<PopoDbContext> contextFactory)
    : IBondDailyPriceHistoryProvider
{
    public async Task<DateOnly?> GetLatestTradingDateBeforeAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MoexHistoryYieldsEntities
            .AsNoTracking()
            .Where(price => price.TradeDate < date && price.LegalClosePrice > 0 && price.LegalClosePrice <= double.MaxValue)
            .OrderByDescending(price => price.TradeDate)
            .Select(price => (DateOnly?)price.TradeDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BondDailyPricePoint>> GetClosingPricesAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var prices = await (
            from price in context.MoexHistoryYieldsEntities.AsNoTracking()
            where price.TradeDate == date && price.LegalClosePrice > 0 && price.LegalClosePrice <= double.MaxValue
            join bond in context.MoexBonds.AsNoTracking()
                on price.SecId equals bond.SecId into bondGroup
            from bond in bondGroup.DefaultIfEmpty()
            select new
            {
                price.SecId,
                price.BoardId,
                ShortName = price.ShortName ?? price.SecId,
                Price = price.LegalClosePrice!.Value,
                price.NumTrades,
                price.Value,
                price.CurrencyId,
                MoexType = bond == null ? null : bond.Type,
                EmitterId = bond == null ? null : bond.EmitterId
            })
            .ToListAsync(cancellationToken);

        return prices.Select(price => new BondDailyPricePoint(
            price.SecId,
            price.BoardId,
            price.ShortName,
            price.Price,
            price.NumTrades,
            price.Value,
            price.CurrencyId,
            BondIssuerCategoryHelper.GetIssuerCategory(price.MoexType),
            price.EmitterId)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, BondDailySecurityMetadata>> GetSecurityMetadataAsync(
        IReadOnlyCollection<string> secIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secIds);
        if (secIds.Count == 0)
            return new Dictionary<string, BondDailySecurityMetadata>();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var bonds = await context.MoexBonds
            .AsNoTracking()
            .Where(bond => secIds.Contains(bond.SecId))
            .Select(bond => new { bond.SecId, bond.ShortName, bond.Type, bond.EmitterId })
            .ToListAsync(cancellationToken);

        return bonds
            .ToDictionary(
                bond => bond.SecId,
                bond => new BondDailySecurityMetadata(
                    bond.ShortName,
                    BondIssuerCategoryHelper.GetIssuerCategory(bond.Type),
                    bond.EmitterId),
                StringComparer.OrdinalIgnoreCase);
    }
}
