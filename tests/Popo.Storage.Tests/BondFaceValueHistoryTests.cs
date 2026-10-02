using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Bonds;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers.Bonds;

namespace Popo.Storage.Tests;

public sealed class BondFaceValueHistoryTests
{
    [Test]
    public async Task GetHistoricalFaceValuesAsync_UsesLatestPositiveValueOnOrBeforeEachTradeDateAndBoard()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var factory = database.ContextFactory;
        var secId = $"face-value-{Guid.NewGuid():N}";
        var firstDate = new DateOnly(2024, 5, 3);
        var reducedDate = new DateOnly(2024, 6, 20);
        var futureDate = new DateOnly(2024, 7, 2);
        var storedSecId = secId.ToUpperInvariant();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.MoexHistoryYieldsEntities.AddRange(
                new MoexHistoryYieldsEntity { SecId = storedSecId, BoardId = "TQCB", TradeDate = firstDate, FaceValue = 500 },
                new MoexHistoryYieldsEntity { SecId = storedSecId, BoardId = "TQCB", TradeDate = reducedDate, FaceValue = 250 },
                new MoexHistoryYieldsEntity { SecId = storedSecId, BoardId = "TQCB", TradeDate = futureDate, FaceValue = 100 },
                new MoexHistoryYieldsEntity { SecId = storedSecId, BoardId = "TQOB", TradeDate = reducedDate, FaceValue = 900 });
            await db.SaveChangesAsync();
        }

        var provider = new BondsProvider(factory);
        var result = await provider.GetHistoricalFaceValuesAsync(
        [
            new BondFaceValueRequest(secId.ToLowerInvariant(), "tqcb", new DateOnly(2024, 6, 1)),
            new BondFaceValueRequest(secId, "TQCB", new DateOnly(2024, 6, 30)),
            new BondFaceValueRequest(secId, "TQOB", new DateOnly(2024, 6, 1)),
            new BondFaceValueRequest(secId, "TQCB", new DateOnly(2024, 4, 30))
        ], CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(x => x.FaceValue), Is.EqualTo(new double?[] { 500, 250, null, null }));
            Assert.That(result.Select(x => x.TradeDate), Is.EqualTo(new DateOnly[]
            {
                new(2024, 6, 1), new(2024, 6, 30), new(2024, 6, 1), new(2024, 4, 30)
            }));
        });
    }
}
