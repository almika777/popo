using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.PortfolioReturns;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioBondBenchmarkHistoryProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioBondBenchmarkHistoryProvider _provider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _provider = new PortfolioBondBenchmarkHistoryProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task GetHistoryAsync_ReturnsHistoricalAccruedInterestAndCouponPeriodsThroughNextPayment()
    {
        var secId = $"BOND-{Guid.NewGuid():N}";
        var isin = $"ISIN-{Guid.NewGuid():N}";
        const string boardId = "TQCB";
        var from = new DateOnly(2026, 8, 10);
        var through = new DateOnly(2026, 10, 8);
        var tradeDate = new DateOnly(2026, 8, 7);
        var couponBetweenQuoteAndStart = new DateOnly(2026, 8, 8);
        var previousCouponDate = new DateOnly(2026, 9, 30);
        var couponDate = new DateOnly(2026, 11, 19);
        await using (var context = await _database.ContextFactory.CreateDbContextAsync())
        {
            context.MoexBondSecurities.Add(new MoexBondSecurityEntity
            {
                SecId = secId,
                Isin = isin,
                BoardId = boardId,
                FaceUnit = "RUB",
                CurrencyId = "RUB"
            });
            context.MoexHistoryYieldsEntities.Add(new MoexHistoryYieldsEntity
            {
                SecId = secId,
                BoardId = boardId,
                TradeDate = tradeDate,
                Close = 100,
                LegalClosePrice = 100,
                FaceValue = 1_025,
                CouponPercent = 1.85,
                AccInt = 4.75
            });
            context.MoexCouponsEntities.AddRange(
                Coupon(secId, isin, boardId, new DateOnly(2026, 6, 1), new DateOnly(2025, 12, 1), 10, 10),
                Coupon(secId, isin, boardId, couponBetweenQuoteAndStart, new DateOnly(2026, 5, 15), 10, 10),
                Coupon(secId, isin, "CETS", previousCouponDate, new DateOnly(2026, 5, 15), 10.06, 0),
                Coupon(secId, isin, boardId, couponDate, previousCouponDate, 10.06, 10.25),
                Coupon(secId, isin, boardId, new DateOnly(2027, 5, 19), couponDate, 12, null));
            await context.SaveChangesAsync();
        }

        var result = await _provider.GetHistoryAsync(secId, boardId, from, through, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Points.Single().AccruedInterest, Is.EqualTo(4.75));
            Assert.That(result.CouponPeriods.Select(period => period.CouponDate), Is.EqualTo(new[] { couponBetweenQuoteAndStart, previousCouponDate, couponDate }));
            Assert.That(result.CouponPeriods.Select(period => period.AmountInRub), Is.EqualTo(new double?[] { 10, 10.06, 10.25 }));
        });
    }

    private static MoexCouponsEntity Coupon(
        string secId,
        string isin,
        string boardId,
        DateOnly couponDate,
        DateOnly periodStartDate,
        double? value,
        double? valueInRub) => new()
    {
        SecId = secId,
        Isin = isin,
        BoardId = boardId,
        CouponDate = couponDate,
        StartDate = periodStartDate,
        RecordDate = couponDate.AddDays(-1),
        InitialFaceValue = 1_000,
        FaceValue = 1_025,
        FaceUnit = "RUB",
        Value = value,
        ValueInRub = valueInRub
    };
}
