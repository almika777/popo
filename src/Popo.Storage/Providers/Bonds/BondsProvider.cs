using Microsoft.EntityFrameworkCore;
using Popo.Core.Bonds;

namespace Popo.Storage.Providers.Bonds;

public sealed class BondsProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IBondsProvider
{
    public async Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoexBondSecurities
            .AsNoTracking()
            .Where(x => x.CurrencyId != null && x.CurrencyId != string.Empty)
            .Select(x => x.CurrencyId!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoexBondSecurities
            .AsNoTracking()
            .Where(x => x.FaceUnit != null && x.FaceUnit != string.Empty)
            .Select(x => x.FaceUnit!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<BondSecurityDto>> GetSecurities(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoexBondSecurities
            .Join(dbContext.MoexBonds, x => x.Isin, x => x.Isin, (entity, bondEntity) => new BondSecurityDto(
                entity.SecId, 
                entity.Isin, 
                entity.BoardId, 
                entity.BondType, 
                entity.FaceUnit, 
                entity.FaceValueOnSettleDate ?? entity.FaceValue,
                entity.CurrencyId,
                bondEntity.EmitterId,
                dbContext.MoexEmitents
                    .Where(x => x.Id == bondEntity.EmitterId)
                    .Select(x => x.Title)
                    .FirstOrDefault(),
                bondEntity.CouponFrequency,
                entity.SettleDate,
                entity.OfferDate,
                entity.MatDate,
                entity.AccruedInt,
                entity.BuybackDate,
                entity.BuybackPrice,
                entity.CallOptionDate,
                entity.PutOptionDate,
                entity.BondSubType,
                entity.ShortName))
            .ToListAsync(cancellationToken: cancellationToken);
    }
    
    public async Task<IReadOnlyList<BondSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pattern = $"%{query}%";

        return await dbContext.MoexBondSecurities
            .AsNoTracking()
            .Where(x => EF.Functions.ILike(x.SecId, pattern)
                        || EF.Functions.ILike(x.ShortName, pattern)
                        || (x.SecName != null && EF.Functions.ILike(x.SecName, pattern)))
            .OrderBy(x => x.SecId)
            .ThenBy(x => x.BoardId)
            .Select(x => new BondSearchResult(
                x.SecId,
                x.BoardId,
                x.CurrencyId ?? string.Empty,
                x.ShortName,
                x.AccruedInt ?? 0,
                x.FaceValue))
            .ToListAsync(cancellationToken);
    }
}
