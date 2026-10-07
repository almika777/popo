using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioCouponProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioCouponProvider _provider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _provider = new PortfolioCouponProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task CouponSchedule_IncludesFutureRowsAndFiltersByFullBondIdentity()
    {
        var secId = $"FLOAT-{Guid.NewGuid():N}";
        const string isin = "RU000A10FLOAT";
        const string positionBoardId = "TQCB";
        const string primaryBoardId = "TQBR";
        var historicalDate = new DateOnly(2026, 6, 30);
        var futureDate = new DateOnly(2026, 9, 30);
        await using (var context = await _database.ContextFactory.CreateDbContextAsync())
        {
            context.MoexCouponsEntities.AddRange(
                Coupon(secId, isin, primaryBoardId, historicalDate, new DateOnly(2026, 1, 1), 50),
                Coupon(secId, isin, primaryBoardId, futureDate, new DateOnly(2026, 7, 1), null),
                Coupon(secId, "OTHER-ISIN", primaryBoardId, new DateOnly(2026, 12, 31), new DateOnly(2026, 10, 1), 75),
                Coupon($"OTHER-{secId}", isin, primaryBoardId, historicalDate, new DateOnly(2026, 1, 1), 90));
            await context.SaveChangesAsync();
        }

        var bond = new PortfolioBondKey(secId, isin, positionBoardId);
        var schedule = await _provider.GetCouponScheduleAsync([bond], CancellationToken.None);
        var history = await _provider.GetPublishedHistoryAsync(
            [bond], historicalDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.Select(x => x.Date), Is.EqualTo(new[] { historicalDate, futureDate }));
            Assert.That(schedule.Single(x => x.Date == futureDate).Value, Is.Null);
            Assert.That(history.Select(x => x.Date), Is.EqualTo(new[] { historicalDate }));
        });
    }

    private static MoexCouponsEntity Coupon(
        string secId,
        string isin,
        string boardId,
        DateOnly couponDate,
        DateOnly periodStart,
        double? value) => new()
    {
        SecId = secId,
        Isin = isin,
        BoardId = boardId,
        CouponDate = couponDate,
        StartDate = periodStart,
        InitialFaceValue = 1_000,
        FaceValue = 1_000,
        FaceUnit = "RUB",
        Value = value
    };
}
