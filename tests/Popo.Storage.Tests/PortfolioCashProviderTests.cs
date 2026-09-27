using NUnit.Framework;
using Popo.Core.Portfolio;
using Popo.Storage.Providers.Portfolio;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class PortfolioCashProviderTests
{
    private IsolatedTestDatabase _database = null!;
    private PortfolioCashProvider _cashProvider = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateIsolatedAsync();
        _cashProvider = new PortfolioCashProvider(_database.ContextFactory);
    }

    [OneTimeTearDown]
    public ValueTask OneTimeTearDown() => _database.DisposeAsync();

    [Test]
    public async Task CashSnapshot_CanBeAddedUpdatedAndDeleted()
    {
        var created = await _cashProvider.AddCashSnapshotAsync(
            new CashSnapshot("RUB", new DateOnly(2026, 1, 1), 100_000),
            "start",
            CancellationToken.None);

        var snapshots = await _cashProvider.GetCashSnapshotsAsync(CancellationToken.None);
        Assert.That(snapshots, Has.Count.EqualTo(1));
        Assert.That(snapshots.Single().Amount, Is.EqualTo(100_000));

        var updated = await _cashProvider.UpdateCashSnapshotAsync(
            created.Id,
            new CashSnapshot("RUB", new DateOnly(2026, 1, 1), 90_000),
            "updated",
            CancellationToken.None);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Amount, Is.EqualTo(90_000));
        Assert.That((await _cashProvider.GetCashSnapshotAsync(created.Id, CancellationToken.None))!.Amount, Is.EqualTo(90_000));
        Assert.That(await _cashProvider.DeleteCashSnapshotAsync(created.Id, CancellationToken.None), Is.True);
    }
}
