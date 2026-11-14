using NUnit.Framework;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioCouponProviderTests
{
    [Test]
    public async Task GetCouponScheduleAsync_ReturnsRequestedHistoryAndCurrentPeriod()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var secId = $"BOND-{Guid.NewGuid():N}";
        var otherId = $"BOND-{Guid.NewGuid():N}";
        var start = new DateOnly(2026, 1, 1);
        await using (var context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.MoexCouponsEntities.AddRange(
                Coupon(secId, start, start.AddDays(30), 30),
                Coupon(secId, start.AddDays(30), start.AddDays(60), null),
                Coupon(secId, start.AddDays(60), start.AddDays(90), 90),
                Coupon(otherId, start, start.AddDays(30), 50));
            await context.SaveChangesAsync();
        }

        var result = await new PortfolioCouponProvider(database.ContextFactory)
            .GetCouponScheduleAsync([secId], start.AddDays(40), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(coupon => coupon.SecId), Is.EqualTo(new[] { secId, secId }));
            Assert.That(result.Select(coupon => coupon.CouponDate), Is.EqualTo(new[] { start.AddDays(30), start.AddDays(60) }));
            Assert.That(result.Select(coupon => coupon.Value), Is.EqualTo(new double?[] { 30, null }));
            Assert.That(result.Select(coupon => coupon.FaceUnit), Is.EqualTo(new[] { "RUB", "RUB" }));
        });
    }

    private static MoexCouponsEntity Coupon(string secId, DateOnly start, DateOnly end, double? value) => new()
    {
        SecId = secId, Isin = secId, BoardId = "TQCB", StartDate = start, CouponDate = end,
        FaceUnit = "RUB", InitialFaceValue = 1_000, FaceValue = 1_000, Value = value
    };
}
