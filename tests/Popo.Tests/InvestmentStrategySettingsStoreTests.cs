using NUnit.Framework;
using Popo.Storage.Entities;
using Popo.Storage.Providers;

namespace Popo.Tests;

[TestFixture]
public sealed class InvestmentStrategySettingsStoreTests
{
    [TestCase(1d)]
    [TestCase(100d)]
    [TestCase(1_000d)]
    public void ToDomain_PreservesMinimumMedianDailyVolume(double storedValue)
    {
        var entity = new InvestmentStrategySettingsEntity
        {
            MinimumMedianDailyVolume = storedValue
        };

        var settings = InvestmentStrategySettingsStore.ToDomain(entity);

        Assert.That(settings.MinimumMedianDailyVolume, Is.EqualTo(storedValue));
    }
}
