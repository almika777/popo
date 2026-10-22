using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.ClosedSessions;

namespace Popo.Tests;

[TestFixture]
public sealed class ClosedPositionSessionsServiceTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [Test]
    public void Recalculate_DoesNotPublishPartialResultsWhenAnyInstrumentFails()
    {
        var existing = Session(Guid.NewGuid(), "EXISTING", 100);
        var valid = Instrument("VALID", [Trade(0, TradeSide.Buy, 1), Trade(1, TradeSide.Sell, 1)]);
        var invalid = Instrument("INVALID", [Trade(0, TradeSide.Buy, 1), Trade(1, TradeSide.Sell, 2)]);
        var source = new FakeSourceProvider([valid, invalid]);
        var sessions = new FakeSessionsProvider([existing]);
        var service = new ClosedPositionSessionsService(source, sessions);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RecalculateAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(sessions.ReplaceCalls, Is.Zero);
            Assert.That(sessions.Snapshot, Is.EqualTo(new[] { existing }));
        });
    }

    [Test]
    public async Task Recalculate_PublishesAllInstrumentResultsOnceWithSharedCalculatedAt()
    {
        var source = new FakeSourceProvider([
            Instrument("FIRST", [Trade(0, TradeSide.Buy, 1, 100), Trade(2, TradeSide.Sell, 1, 110)]),
            Instrument("SECOND", [Trade(0, TradeSide.Buy, 2, 200), Trade(3, TradeSide.Sell, 2, 215)])
        ]);
        var sessions = new FakeSessionsProvider([]);
        var service = new ClosedPositionSessionsService(source, sessions);

        var calculatedCount = await service.RecalculateAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(calculatedCount, Is.EqualTo(2));
            Assert.That(sessions.ReplaceCalls, Is.EqualTo(1));
            Assert.That(sessions.Snapshot, Has.Count.EqualTo(2));
            Assert.That(sessions.Snapshot.Select(x => x.SecId), Is.EquivalentTo(new[] { "FIRST", "SECOND" }));
            Assert.That(sessions.Snapshot.Single(x => x.SecId == "FIRST").TotalPnl, Is.EqualTo(10d));
            Assert.That(sessions.Snapshot.Single(x => x.SecId == "SECOND").TotalPnl, Is.EqualTo(30d));
            Assert.That(sessions.Snapshot.Select(x => x.CalculatedAt).Distinct().Count(), Is.EqualTo(1));
        });
    }

    private static ClosedSessionInstrument Instrument(string secId, IReadOnlyList<ClosedSessionTrade> trades)
        => new(secId, "TQCB", "RUB", $"{secId} bond", "RUB", "", "", 0, 1_000, null,
            trades, [], [], []);

    private static ClosedSessionTrade Trade(int day, TradeSide side, double quantity, double price = 100)
        => new(Guid.NewGuid(), Start.AddDays(day), side, quantity, price, 0);

    private static ClosedPositionSession Session(Guid id, string secId, double totalPnl) => new(
        id, secId, "TQCB", "RUB", secId, Start, Start.AddDays(1), 1, 1, 100, 100, 0,
        0, 0, 0, totalPnl, totalPnl, totalPnl * 365, true, null, "Default", DateTimeOffset.UtcNow);

    private sealed class FakeSourceProvider(IReadOnlyList<ClosedSessionInstrument> instruments)
        : IClosedPositionSessionSourceProvider
    {
        public Task<IReadOnlyList<ClosedSessionInstrument>> GetAsync(DateOnly asOf,
            CancellationToken cancellationToken) => Task.FromResult(instruments);
    }

    private sealed class FakeSessionsProvider(IReadOnlyList<ClosedPositionSession> initialSnapshot)
        : IClosedPositionSessionsProvider
    {
        public IReadOnlyList<ClosedPositionSession> Snapshot { get; private set; } = initialSnapshot;

        public int ReplaceCalls { get; private set; }

        public Task<IReadOnlyList<ClosedPositionSession>> GetAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task ReplaceAsync(IReadOnlyList<ClosedPositionSession> sessions, CancellationToken cancellationToken)
        {
            ReplaceCalls++;
            Snapshot = sessions.ToArray();
            return Task.CompletedTask;
        }
    }
}
