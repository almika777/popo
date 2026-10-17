using System.Data;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio.ClosedSessions;

namespace Popo.Storage.Providers.Portfolio;

public sealed class ClosedPositionSessionSourceProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IClosedPositionSessionSourceProvider
{
    public async Task<IReadOnlyList<ClosedSessionInstrument>> GetAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var trades = await db.PortfolioTrades.AsNoTracking().Where(x => x.TradeDate <= asOf)
            .OrderBy(x => x.TradeDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        if (trades.Count == 0)
            return [];

        var ids = trades.Select(x => x.SecId).Distinct().ToArray();
        var bonds = await db.MoexBonds.AsNoTracking().Where(x => ids.Contains(x.SecId)).ToListAsync(cancellationToken);
        var securities = await db.MoexBondSecurities.AsNoTracking().Where(x => ids.Contains(x.SecId))
            .ToListAsync(cancellationToken);
        // Будущая дата выплаты нужна, если сессия закончилась внутри этого купонного периода.
        var coupons = await db.MoexCouponsEntities.AsNoTracking().Where(x => ids.Contains(x.SecId))
            .ToListAsync(cancellationToken);
        var amorts = await db.MoexAmortsEntities.AsNoTracking().Where(x => ids.Contains(x.SecId) && x.AmortDate <= asOf)
            .ToListAsync(cancellationToken);
        var currencies = trades.Select(x => x.CurrencyId).Concat(coupons.Select(x => x.FaceUnit))
            .Concat(amorts.Select(x => x.FaceUnit)).Distinct().ToArray();
        var rates = await db.CurrencyRates.AsNoTracking()
            .Where(x => currencies.Contains(x.CurrencyCode) && x.RateDate <= asOf)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var currencyRates = rates.Select(x => new ClosedSessionCurrencyRate(x.RateDate, x.CurrencyCode,
            (decimal)x.UnitRate)).ToArray();
        var results = new List<ClosedSessionInstrument>();
        foreach (var group in trades.GroupBy(x => (x.SecId, x.BoardId, x.CurrencyId)))
        {
            var bond = bonds.FirstOrDefault(x => x.SecId == group.Key.SecId);
            var security = securities.Where(x => x.SecId == group.Key.SecId)
                .OrderByDescending(x => x.BoardId == group.Key.BoardId).ThenBy(x => x.BoardId).FirstOrDefault();
            var schedule = coupons.Where(x => x.SecId == group.Key.SecId).OrderBy(x => x.CouponDate).ToArray();
            var amortizations = amorts.Where(x => x.SecId == group.Key.SecId).OrderBy(x => x.AmortDate).ToArray();
            var faceUnit = bond?.FaceUnit ?? security?.FaceUnit ?? schedule.FirstOrDefault()?.FaceUnit ?? "";
            var initial = bond?.InitialFaceValue ?? schedule.FirstOrDefault()?.InitialFaceValue
                ?? amortizations.FirstOrDefault()?.InitialFaceValue ?? 0;
            results.Add(new ClosedSessionInstrument(group.Key.SecId, group.Key.BoardId, group.Key.CurrencyId,
                bond?.ShortName ?? security?.ShortName ?? group.Key.SecId, faceUnit,
                bond?.BondType ?? security?.BondType ?? "", bond?.BondSubType ?? security?.BondSubType ?? "",
                bond?.CouponFrequency, (decimal)initial, bond?.MatDate ?? security?.MatDate,
                group.Select(x => new ClosedSessionTrade(x.Id, x.TradeDate, x.Side, (decimal)x.Quantity,
                    (decimal)x.Price, (decimal)x.Commission)).ToArray(),
                schedule.Select(x => new ClosedSessionCoupon(x.StartDate, x.CouponDate,
                    (decimal?)x.Value, (decimal?)x.ValueInRub, x.FaceUnit)).ToArray(),
                amortizations.Select(x => new ClosedSessionAmortization(x.AmortDate,
                    (decimal?)x.Value, (decimal?)x.ValueInRub, (decimal?)x.ValuePercent,
                    (decimal)x.InitialFaceValue, x.FaceUnit)).ToArray(), currencyRates));
        }
        return results;
    }
}
