using System.Data;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio.ClosedSessions;
using Popo.Storage.Entities;
using Popo.Storage.Entities.Moex;

namespace Popo.Storage.Providers.Portfolio;

public sealed class ClosedPositionSessionSourceProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IClosedPositionSessionSourceProvider
{
    /// <summary>Читает согласованные сделки и связанные данные для расчёта сессий на указанную дату.</summary>
    public async Task<IReadOnlyList<ClosedSessionInstrument>> GetAsync(DateOnly asOf,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);

        var sourceData = await LoadSourceDataAsync(db, asOf, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return BuildInstruments(sourceData);
    }

    private static async Task<ClosedSessionSourceData> LoadSourceDataAsync(
        PopoDbContext db,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var trades = await db.PortfolioTrades
            .Where(x => x.TradeDate <= asOf)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (trades.Count == 0)
            return new ClosedSessionSourceData([], [], [], [], [], []);

        var secIds = trades.Select(x => x.SecId).Distinct().ToArray();

        var bonds = await db.MoexBonds
            .Where(x => secIds.Contains(x.SecId))
            .ToListAsync(cancellationToken);

        var securities = await db.MoexBondSecurities
            .Where(x => secIds.Contains(x.SecId))
            .ToListAsync(cancellationToken);

        // Будущая дата выплаты нужна, если сессия закончилась внутри этого купонного периода.
        var coupons = await db.MoexCouponsEntities
            .Where(x => secIds.Contains(x.SecId))
            .ToListAsync(cancellationToken);

        var amorts = await db.MoexAmortsEntities
            .Where(x => secIds.Contains(x.SecId) && x.AmortDate <= asOf)
            .ToListAsync(cancellationToken);

        var currencies = trades
            .Select(x => x.CurrencyId)
            .Concat(coupons.Select(x => x.FaceUnit))
            .Concat(amorts.Select(x => x.FaceUnit))
            .Distinct()
            .ToArray();

        var rates = await db.CurrencyRates
            .Where(x => currencies.Contains(x.CurrencyCode) && x.RateDate <= asOf)
            .ToListAsync(cancellationToken);

        return new(trades, bonds, securities, coupons, amorts, rates);
    }

    private static IReadOnlyList<ClosedSessionInstrument> BuildInstruments(ClosedSessionSourceData sourceData)
    {
        var bondsBySecId = sourceData.Bonds.ToDictionary(x => x.SecId);
        var securitiesBySecId = sourceData.Securities
            .GroupBy(x => x.SecId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.BoardId).ToArray());
        var couponsBySecId = sourceData.Coupons
            .GroupBy(x => x.SecId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.CouponDate).ToArray());
        var amortizationsBySecId = sourceData.Amortizations
            .GroupBy(x => x.SecId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.AmortDate).ToArray());
        var currencyRates = sourceData.Rates
            .Select(x => new ClosedSessionCurrencyRate(x.RateDate, x.CurrencyCode, x.UnitRate))
            .ToArray();

        var results = new List<ClosedSessionInstrument>();
        foreach (var group in sourceData.Trades.GroupBy(x => (x.SecId, x.BoardId, x.CurrencyId)))
        {
            var bond = bondsBySecId.GetValueOrDefault(group.Key.SecId);
            var securities = securitiesBySecId.GetValueOrDefault(group.Key.SecId) ?? [];
            var security = SelectSecurity(securities, group.Key.BoardId);
            var schedule = couponsBySecId.GetValueOrDefault(group.Key.SecId) ?? [];
            var amortizations = amortizationsBySecId.GetValueOrDefault(group.Key.SecId) ?? [];

            var instrument =
                BuildClosedSessionInstrument(group, bond, security, schedule, amortizations, currencyRates);
            results.Add(instrument);
        }

        return results;
    }

    private static MoexBondSecurityEntity? SelectSecurity(
        IReadOnlyList<MoexBondSecurityEntity> securities,
        string boardId)
        => securities.FirstOrDefault(x => x.BoardId == boardId) ?? securities.FirstOrDefault();

    private static ClosedSessionInstrument BuildClosedSessionInstrument(
        IGrouping<(string SecId, string BoardId, string CurrencyId), PortfolioTradeEntity> group,
        MoexBondEntity? bond,
        MoexBondSecurityEntity? security,
        MoexCouponsEntity[] schedule,
        MoexAmortsEntity[] amortizations,
        ClosedSessionCurrencyRate[] currencyRates)
    {
        var faceUnit = bond?.FaceUnit ?? security?.FaceUnit ?? schedule.FirstOrDefault()?.FaceUnit ?? "";
        var initial = bond?.InitialFaceValue ?? schedule.FirstOrDefault()?.InitialFaceValue
            ?? amortizations.FirstOrDefault()?.InitialFaceValue ?? 0;

        return new ClosedSessionInstrument(
            group.Key.SecId,
            group.Key.BoardId,
            group.Key.CurrencyId,
            bond?.ShortName ?? security?.ShortName ?? group.Key.SecId,
            faceUnit,
            bond?.BondType ?? security?.BondType ?? "",
            bond?.BondSubType ?? security?.BondSubType ?? "",
            bond?.CouponFrequency, initial, bond?.MatDate ?? security?.MatDate,
            group.Select(x => new ClosedSessionTrade(x.Id, x.TradeDate, x.Side, x.Quantity,
                x.Price, x.Commission, x.AccruedInterest)).ToArray(),
            schedule.Select(x => new ClosedSessionCoupon(x.StartDate, x.CouponDate,
                x.Value, x.ValueInRub, x.FaceUnit)).ToArray(),
            amortizations.Select(x => new ClosedSessionAmortization(x.AmortDate,
                x.Value, x.ValueInRub, x.ValuePercent,
                x.InitialFaceValue, x.FaceUnit)).ToArray(), currencyRates);
    }

    private sealed record ClosedSessionSourceData(
        IReadOnlyList<PortfolioTradeEntity> Trades,
        IReadOnlyList<MoexBondEntity> Bonds,
        IReadOnlyList<MoexBondSecurityEntity> Securities,
        IReadOnlyList<MoexCouponsEntity> Coupons,
        IReadOnlyList<MoexAmortsEntity> Amortizations,
        IReadOnlyList<CurrencyRateEntity> Rates);
}
