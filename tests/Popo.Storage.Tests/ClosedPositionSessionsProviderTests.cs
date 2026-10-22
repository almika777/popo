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
            3.125d, 12345.67890123d, 12001.23456789d, 12.3456789d,
            98.7654321d, -444.44444444d, 333.33333333d, -123.45678901d,
            -1.23456789d, -14.81481468d, true, null, "Floater", calculatedAt))
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
            1.5d, 300, 310, 0.75d, null, null, 12.34d, null, null, null, false,
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
        Assert.That(result[0].TotalPnl, Is.EqualTo(50d));

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
        // Отсутствующий обязательный SECID вызывает ошибку сохранения после удаления внутри транзакции.
        var invalid = Session(Guid.NewGuid(), 20) with { SecId = null! };
        Assert.ThrowsAsync<DbUpdateException>(async () =>
            await provider.ReplaceAsync([invalid], CancellationToken.None));

        var result = await provider.GetAsync(CancellationToken.None);
        Assert.That(result.Select(x => x.Id), Is.EqualTo(new[] { saved.Id }));
        Assert.That(result[0].TotalPnl, Is.EqualTo(10d));
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

    private static ClosedPositionSession Session(Guid id, double coupon) => new(
        id, $"SESSION-{id:N}", "TQCB", "RUB", "Test", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
        30, 1, 1000, 1000, 0, 0, 0, coupon, coupon, coupon / 10, coupon / 10 * 365 / 30,
        true, null, "Default", DateTimeOffset.UtcNow);
}
