using Microsoft.EntityFrameworkCore;
using Popo.Core.Bonds;

namespace Popo.Storage.Providers.Bonds;

public sealed class BondsProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IBondsProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoexBondSecurities
            .AsNoTracking()
            .Where(x => x.CurrencyId != null && x.CurrencyId != string.Empty)
            .Select(x => x.CurrencyId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.MoexBondSecurities
            .AsNoTracking()
            .Where(x => x.FaceUnit != null && x.FaceUnit != string.Empty)
            .Select(x => x.FaceUnit)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<BondSecurityDto>> GetSecuritiesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var securities =
            from security in dbContext.MoexBondSecurities
            join bond in dbContext.MoexBonds.AsNoTracking()
                on security.SecId equals bond.SecId
            join issuer in dbContext.MoexEmitents
                on bond.EmitterId equals issuer.Id into issuerGroup
            from issuer in issuerGroup.DefaultIfEmpty()
            select new BondSecurityDto(
                SecId: security.SecId,
                Isin: security.Isin,
                BoardId: security.BoardId,
                BondType: security.BondType,
                FaceUnit: security.FaceUnit,
                FaceValue: security.FaceValueOnSettleDate ?? security.FaceValue,
                CurrencyId: security.CurrencyId,
                EmitterId: bond.EmitterId,
                IssuerName: issuer == null ? null : issuer.Title,
                CouponFrequency: bond.CouponFrequency,
                SettleDate: security.SettleDate,
                OfferDate: security.OfferDate,
                MatDate: security.MatDate,
                AccruedInterest: security.AccruedInt,
                BuybackDate: security.BuybackDate,
                BuybackPrice: security.BuybackPrice,
                CallOptionDate: security.CallOptionDate,
                PutOptionDate: security.PutOptionDate,
                BondSubType: security.BondSubType,
                ShortName: security.ShortName);

        return await securities.ToListAsync(cancellationToken);
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
                x.CurrencyId,
                x.ShortName,
                x.AccruedInt,
                x.FaceValue))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
        IReadOnlyList<BondFaceValueRequest> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return [];

        var normalizedRequests = requests.Select(request => request with
        {
            SecId = request.SecId.Trim().ToUpperInvariant(),
            BoardId = request.BoardId.Trim().ToUpperInvariant()
        }).ToArray();
        var secIds = normalizedRequests.Select(x => x.SecId).Distinct().ToArray();
        var latestRequestedDate = normalizedRequests.Max(x => x.TradeDate);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var history = await dbContext.MoexHistoryYieldsEntities
            .AsNoTracking()
            .Where(x => secIds.Contains(x.SecId)
                        && x.TradeDate <= latestRequestedDate
                        && x.FaceValue > 0)
            .Select(x => new HistoricalFaceValuePoint(x.SecId, x.BoardId, x.TradeDate, x.FaceValue))
            .ToListAsync(cancellationToken);

        return normalizedRequests.Select(request =>
        {
            var faceValue = history
                .Where(x => x.SecId == request.SecId
                            && x.BoardId == request.BoardId
                            && x.TradeDate <= request.TradeDate
                            && double.IsFinite(x.FaceValue))
                .OrderByDescending(x => x.TradeDate)
                .Select(x => (double?)x.FaceValue)
                .FirstOrDefault();

            return new BondFaceValueResult(
                request.SecId, request.BoardId, request.TradeDate, faceValue);
        }).ToArray();
    }

    private sealed record HistoricalFaceValuePoint(
        string SecId,
        string BoardId,
        DateOnly TradeDate,
        double FaceValue);
}
