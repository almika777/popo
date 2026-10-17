using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Portfolio.ClosedSessions;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class ClosedPositionSessionsProviderTests
{
    [TestCaseSource(nameof(SessionRoundTripCases))]
    public async Task ReplaceAndGet_PreserveEverySessionField(ClosedPositionSession expected)
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var provider = new ClosedPositionSessionsProvider(database.ContextFactory);

        await provider.ReplaceAsync([expected], CancellationToken.None);

        Assert.That(await provider.GetAsync(CancellationToken.None), Is.EqualTo(new[] { expected }));
    }

    private static IEnumerable<TestCaseData> SessionRoundTripCases()
    {
        var calculatedAt = new DateTimeOffset(2026, 10, 15, 12, 34, 56, TimeSpan.Zero);
        yield return new TestCaseData(new ClosedPositionSession(
            Guid.Parse("10000000-0000-0000-0000-000000000001"), "ROUNDTRIP-COMPLETE", "TQCB", "RUB",
            "Complete session", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 31,
            3.125m, 12345.67890123m, 12001.23456789m, 12.3456789m,
            98.7654321m, -444.44444444m, 333.33333333m, -123.45678901m,
            -1.23456789m, -14.81481468m, true, null, "Floater", calculatedAt))
            .SetName("Round_trip_complete_session_all_fields");

        yield return new TestCaseData(new ClosedPositionSession(
            Guid.Parse("10000000-0000-0000-0000-000000000002"), "ROUNDTRIP-NO-COUPON", "TQCB", "RUB",
            "Missing coupon", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 11), 10,
            2, 2000, 2100, 5, 0, 100, null, null, null, null, false,
            "Купонный график отсутствует.", "Default", calculatedAt))
            .SetName("Round_trip_incomplete_session_with_null_coupon_and_totals");

        yield return new TestCaseData(new ClosedPositionSession(
            Guid.Parse("10000000-0000-0000-0000-000000000003"), "ROUNDTRIP-NO-AMORT", "TQCB", "RUB",
            "Missing amortization", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 21), 20,
            1.5m, 300, 310, 0.75m, null, null, 12.34m, null, null, null, false,
            "Неизвестна сумма амортизации или курс.", "Zo", calculatedAt))
            .SetName("Round_trip_incomplete_session_with_null_amortization_and_body");
    }

    [Test]
    public async Task Replace_UpdatesResultsRemovesObsoleteSessionsAndDoesNotDuplicateRows()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var provider = new ClosedPositionSessionsProvider(database.ContextFactory);
        var first = Session(Guid.NewGuid(), 10);
        var deleted = Session(Guid.NewGuid(), 20);
        await provider.ReplaceAsync([first, deleted], CancellationToken.None);

        var corrected = first with { TotalPnl = 50, TotalPnlPercent = 5, CouponIncome = 50 };
        await provider.ReplaceAsync([corrected], CancellationToken.None);
        await provider.ReplaceAsync([corrected], CancellationToken.None);
        var result = await provider.GetAsync(CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(first.Id));
        Assert.That(result[0].TotalPnl, Is.EqualTo(50m));

        await provider.ReplaceAsync([], CancellationToken.None);
        Assert.That(await provider.GetAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task Replace_FailedPublicationRollsBackToPreviousSnapshot()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        var provider = new ClosedPositionSessionsProvider(database.ContextFactory);
        var saved = Session(Guid.NewGuid(), 10);
        await provider.ReplaceAsync([saved], CancellationToken.None);
        // numeric(28,8) не вместит decimal.MaxValue: ошибка возникает после удаления внутри транзакции.
        var invalid = Session(Guid.NewGuid(), 20) with { BoughtCleanAmount = decimal.MaxValue };
        Assert.ThrowsAsync<DbUpdateException>(async () =>
            await provider.ReplaceAsync([invalid], CancellationToken.None));

        var result = await provider.GetAsync(CancellationToken.None);
        Assert.That(result.Select(x => x.Id), Is.EqualTo(new[] { saved.Id }));
        Assert.That(result[0].TotalPnl, Is.EqualTo(10m));
    }

    [Test]
    public async Task Migration_CreatesClosedSessionsTable()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var provider = new ClosedPositionSessionsProvider(database.ContextFactory);
        var session = Session(Guid.NewGuid(), 10);
        await provider.ReplaceAsync([session], CancellationToken.None);
        Assert.That((await provider.GetAsync(CancellationToken.None)).Single().Id, Is.EqualTo(session.Id));
    }

    private static ClosedPositionSession Session(Guid id, decimal coupon) => new(
        id, $"SESSION-{id:N}", "TQCB", "RUB", "Test", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
        30, 1, 1000, 1000, 0, 0, 0, coupon, coupon, coupon / 10, coupon / 10 * 365 / 30,
        true, null, "Default", DateTimeOffset.UtcNow);
}
