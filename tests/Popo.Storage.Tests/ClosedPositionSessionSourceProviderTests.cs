using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.ClosedSessions;
using Popo.Storage.Entities;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class ClosedPositionSessionSourceProviderTests
{
    [Test]
    public async Task Recalculate_PersistsSessionWithCouponAmortizationAndCommissions()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var secId = $"SOURCE-COMPLETE-{Guid.NewGuid():N}";
        var buyId = Guid.NewGuid();
        var buyDate = new DateOnly(2026, 1, 1);
        var sellDate = new DateOnly(2026, 2, 1);

        await using (var db = await database.ContextFactory.CreateDbContextAsync())
        {
            db.MoexBonds.Add(Bond(secId, "RUB", 1_000, 12));
            db.PortfolioTrades.AddRange(
                Trade(buyId, secId, "TQCB", "RUB", buyDate, TradeSide.Buy, 2, 1_000, 2, 77),
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", sellDate, TradeSide.Sell, 2, 1_100, 3, 88));
            db.MoexCouponsEntities.Add(Coupon(secId, buyDate, new DateOnly(2026, 2, 10), 100, "RUB"));
            db.MoexAmortsEntities.Add(Amortization(secId, new DateOnly(2026, 1, 15), 50, "RUB", 1_000));
            await db.SaveChangesAsync();
        }

        var (_, stored) = await CalculateAndPersistAsync(database, sellDate);

        var result = stored.Single();
        Assert.Multiple(() =>
        {
            // В исходных сделках указан НКД 77 и 88, но расчёт использует только чистую цену и комиссии.
            Assert.That(result.Id, Is.EqualTo(buyId));
            Assert.That(result.SecId, Is.EqualTo(secId));
            Assert.That(result.HoldingDays, Is.EqualTo(31));
            Assert.That(result.BoughtQuantity, Is.EqualTo(2));
            Assert.That(result.BoughtCleanAmount, Is.EqualTo(2_000));
            Assert.That(result.SoldCleanAmount, Is.EqualTo(2_200));
            Assert.That(result.Commission, Is.EqualTo(5));
            Assert.That(result.AmortizationIncome, Is.EqualTo(100));
            Assert.That(result.BodyPnl, Is.EqualTo(300));
            Assert.That(result.CouponIncome, Is.EqualTo(155));
            Assert.That(result.TotalPnl, Is.EqualTo(450));
            Assert.That(result.TotalPnlPercent, Is.EqualTo(22.5));
            Assert.That(result.AnnualizedPnlPercent, Is.EqualTo(264.92));
            Assert.That(result.IsComplete, Is.True);
        });
    }

    [Test]
    public async Task Recalculate_AsOfExcludesFutureTradesAmortizationsAndRatesButLoadsFutureCoupon()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var secId = $"SOURCE-ASOF-{Guid.NewGuid():N}";
        var asOf = new DateOnly(2026, 1, 20);
        var buyDate = new DateOnly(2026, 1, 1);
        var sellDate = new DateOnly(2026, 1, 15);
        var couponDate = new DateOnly(2026, 1, 31);
        var futureDate = new DateOnly(2026, 1, 21);

        await using (var db = await database.ContextFactory.CreateDbContextAsync())
        {
            db.MoexBonds.Add(Bond(secId, "USD", 1_000, 12));
            db.PortfolioTrades.AddRange(
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", buyDate, TradeSide.Buy, 2, 1_000),
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", sellDate, TradeSide.Sell, 2, 1_100),
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", futureDate, TradeSide.Sell, 1, 1_200));
            db.MoexCouponsEntities.Add(Coupon(secId, buyDate, couponDate, 30, "USD"));
            db.MoexAmortsEntities.Add(Amortization(secId, futureDate, 100, "USD", 1_000));
            db.CurrencyRates.AddRange(
                Rate(buyDate, "USD", 90),
                Rate(futureDate, "USD", 1_000));
            await db.SaveChangesAsync();
        }

        var (instruments, stored) = await CalculateAndPersistAsync(database, asOf);
        var instrument = instruments.Single();
        var result = stored.Single();

        Assert.Multiple(() =>
        {
            Assert.That(instrument.Trades, Has.Count.EqualTo(2));
            Assert.That(instrument.Coupons, Has.Count.EqualTo(1));
            Assert.That(instrument.Coupons[0].EndDate, Is.EqualTo(couponDate));
            Assert.That(instrument.Amortizations, Is.Empty);
            Assert.That(instrument.CurrencyRates, Has.Count.EqualTo(1));
            Assert.That(instrument.CurrencyRates[0].UnitRate, Is.EqualTo(90m));
            Assert.That(result.EndDate, Is.EqualTo(sellDate));
            Assert.That(result.AmortizationIncome, Is.EqualTo(0m));
            // 14 held days * 2 bonds * 30 USD / 30 days * 90 RUB/USD.
            Assert.That(result.CouponIncome, Is.EqualTo(2_520m));
            Assert.That(result.TotalPnl, Is.EqualTo(2_720m));
            Assert.That(result.IsComplete, Is.True);
        });
    }

    [Test]
    public async Task Recalculate_SeparatesSessionsBySecurityBoardAndCurrency()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var secId = $"SOURCE-KEY-{Guid.NewGuid():N}";
        var otherSecId = $"SOURCE-KEY-OTHER-{Guid.NewGuid():N}";
        var buyDate = new DateOnly(2026, 1, 1);
        var sellDate = buyDate.AddDays(1);

        await using (var db = await database.ContextFactory.CreateDbContextAsync())
        {
            db.MoexBonds.Add(Bond(secId, "RUB", 1_000, 0));
            db.MoexBonds.Add(Bond(otherSecId, "RUB", 1_000, 0));
            db.PortfolioTrades.AddRange(
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", buyDate, TradeSide.Buy, 1, 100),
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", sellDate, TradeSide.Sell, 1, 110),
                Trade(Guid.NewGuid(), secId, "TQOB", "RUB", buyDate, TradeSide.Buy, 1, 200),
                Trade(Guid.NewGuid(), secId, "TQOB", "RUB", sellDate, TradeSide.Sell, 1, 220),
                Trade(Guid.NewGuid(), secId, "TQOB", "USD", buyDate, TradeSide.Buy, 1, 300),
                Trade(Guid.NewGuid(), secId, "TQOB", "USD", sellDate, TradeSide.Sell, 1, 330),
                Trade(Guid.NewGuid(), otherSecId, "TQCB", "RUB", buyDate, TradeSide.Buy, 1, 400),
                Trade(Guid.NewGuid(), otherSecId, "TQCB", "RUB", sellDate, TradeSide.Sell, 1, 420));
            await db.SaveChangesAsync();
        }

        var (instruments, stored) = await CalculateAndPersistAsync(database, sellDate);
        var byKey = stored.ToDictionary(x => (x.SecId, x.BoardId, x.CurrencyId));

        Assert.Multiple(() =>
        {
            Assert.That(instruments, Has.Count.EqualTo(4));
            Assert.That(stored, Has.Count.EqualTo(4));
            Assert.That(byKey.Keys, Is.EquivalentTo(new[]
            {
                (secId, "TQCB", "RUB"), (secId, "TQOB", "RUB"), (secId, "TQOB", "USD"),
                (otherSecId, "TQCB", "RUB")
            }));
            Assert.That(byKey[(secId, "TQCB", "RUB")].TotalPnl, Is.EqualTo(10m));
            Assert.That(byKey[(secId, "TQOB", "RUB")].TotalPnl, Is.EqualTo(20m));
            Assert.That(byKey[(secId, "TQOB", "USD")].TotalPnl, Is.EqualTo(30m));
            Assert.That(byKey[(otherSecId, "TQCB", "RUB")].TotalPnl, Is.EqualTo(20m));
        });
    }

    [Test]
    public async Task Recalculate_UsesCurrencyRateUnitRateForDailyCouponConversion()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var secId = $"SOURCE-FX-{Guid.NewGuid():N}";
        var buyDate = new DateOnly(2026, 1, 1);
        var rateChangeDate = new DateOnly(2026, 1, 6);
        var sellDate = new DateOnly(2026, 1, 11);

        await using (var db = await database.ContextFactory.CreateDbContextAsync())
        {
            db.MoexBonds.Add(Bond(secId, "USD", 1_000, 12));
            db.PortfolioTrades.AddRange(
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", buyDate, TradeSide.Buy, 2, 1_000, 2),
                Trade(Guid.NewGuid(), secId, "TQCB", "RUB", sellDate, TradeSide.Sell, 2, 1_100, 3));
            db.MoexCouponsEntities.Add(Coupon(secId, buyDate, new DateOnly(2026, 1, 31), 30, "USD"));
            db.CurrencyRates.AddRange(Rate(buyDate, "USD", 90), Rate(rateChangeDate, "USD", 100));
            await db.SaveChangesAsync();
        }

        var (instruments, stored) = await CalculateAndPersistAsync(database, sellDate);
        var result = stored.Single();

        Assert.Multiple(() =>
        {
            Assert.That(instruments.Single().CurrencyRates.Select(x => x.UnitRate),
                Is.EquivalentTo(new[] { 90m, 100 }));
            // Five days at 90 plus five days at 100, two bonds and one USD coupon per day.
            Assert.That(result.CouponIncome, Is.EqualTo(1_900));
            Assert.That(result.CalculatorKind, Is.EqualTo("Zo"));
            Assert.That(result.BodyPnl, Is.EqualTo(200));
            Assert.That(result.TotalPnl, Is.EqualTo(2_095));
            Assert.That(result.TotalPnlPercent, Is.EqualTo(104.75));
        });
    }

    private static async Task<(IReadOnlyList<ClosedSessionInstrument> Instruments,
        IReadOnlyList<ClosedPositionSession> Sessions)> CalculateAndPersistAsync(
        IsolatedTestDatabase database, DateOnly asOf)
    {
        var source = new ClosedPositionSessionSourceProvider(database.ContextFactory);
        var sessionsProvider = new ClosedPositionSessionsProvider(database.ContextFactory);
        var instruments = await source.GetAsync(asOf, CancellationToken.None);
        var calculatedAt = new DateTimeOffset(asOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var sessions = instruments.SelectMany(instrument =>
            new ClosedPositionSessionCalculator().Calculate(instrument, calculatedAt)).ToArray();
        await sessionsProvider.ReplaceAsync(sessions, CancellationToken.None);
        return (instruments, await sessionsProvider.GetAsync(CancellationToken.None));
    }

    private static MoexBondEntity Bond(string secId, string faceUnit, double initialFaceValue, int frequency) => new()
    {
        Id = Guid.NewGuid(),
        SecId = secId,
        ShortName = $"{secId} bond",
        FaceUnit = faceUnit,
        InitialFaceValue = initialFaceValue,
        CouponFrequency = frequency
    };

    private static PortfolioTradeEntity Trade(Guid id, string secId, string boardId, string currencyId,
        DateOnly date, TradeSide side, double quantity, double price, double commission = 0,
        double accruedInterest = 0) => new()
    {
        Id = id,
        SecId = secId,
        BoardId = boardId,
        CurrencyId = currencyId,
        TradeDate = date,
        Side = side,
        Quantity = quantity,
        Price = price,
        FaceValue = 1_000,
        Commission = commission,
        AccruedInterest = accruedInterest
    };

    private static MoexCouponsEntity Coupon(string secId, DateOnly startDate, DateOnly couponDate,
        double value, string faceUnit) => new()
    {
        SecId = secId,
        Isin = $"ISIN-{secId}",
        StartDate = startDate,
        CouponDate = couponDate,
        InitialFaceValue = 1_000,
        FaceValue = 1_000,
        FaceUnit = faceUnit,
        Value = value,
        BoardId = "TQCB"
    };

    private static MoexAmortsEntity Amortization(string secId, DateOnly date, double value,
        string faceUnit, double initialFaceValue) => new()
    {
        SecId = secId,
        Isin = $"ISIN-{secId}",
        AmortDate = date,
        InitialFaceValue = initialFaceValue,
        FaceValue = initialFaceValue,
        FaceUnit = faceUnit,
        Value = value,
        BoardId = "TQCB"
    };

    private static CurrencyRateEntity Rate(DateOnly date, string currency, double unitRate) => new()
    {
        RateDate = date,
        CurrencyCode = currency,
        Name = currency,
        Nominal = 10,
        Value = unitRate * 10,
        UnitRate = unitRate
    };
}
