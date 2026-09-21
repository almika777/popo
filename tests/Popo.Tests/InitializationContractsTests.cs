using NUnit.Framework;
using Popo.Core.Initialization;

namespace Popo.Tests;

public sealed class InitializationContractsTests
{
    [Test]
    public void Bootstrap_contains_only_the_eight_primary_jobs()
    {
        Assert.That(
            InitializationBootstrap.Jobs.Select(x => x.Key),
            Is.EqualTo(new[]
            {
                "SecUpdateJob",
                "MoexBondSecuritiesUpdateJob",
                "MoexBondUpdateJob",
                "MoexAmortsAndCouponsUpdateJob",
                "MoexHistoryPricesUpdateJob",
                "MoexEmitentUpdateJob",
                "BondRatingUpdateJob",
                "CbrCurrencyRatesUpdateJob"
            }));
        Assert.That(InitializationBootstrap.Jobs.Select(x => x.Key),
            Does.Not.Contain("PositionRecommendationJob"));
    }

    [TestCase(0, 10, 0)]
    [TestCase(5, 10, 50)]
    [TestCase(10, 10, 100)]
    [TestCase(15, 10, 100)]
    public void Job_state_calculates_bounded_percentage(int processed, int total, int expected)
    {
        var state = new InitializationJobState(
            "SecUpdateJob",
            "Активные SEC",
            InitializationJobStatus.Running,
            processed,
            total,
            null,
            1,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        Assert.That(state.ProgressPercent, Is.EqualTo(expected));
    }
}
