using NUnit.Framework;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class PositionRecommendationPolicyTests
{
    private static readonly InvestmentStrategySettings Settings = InvestmentStrategySettings.Defaults;

    [TestCase(79, PositionRecommendationAction.Hold)]
    [TestCase(80, PositionRecommendationAction.Hold)]
    [TestCase(99, PositionRecommendationAction.Hold)]
    [TestCase(100, PositionRecommendationAction.Sell)]
    public void Evaluate_UsesThe8And10PercentAverageVolumeBoundaries(
        double quantity,
        PositionRecommendationAction expected)
    {
        var result = PositionRecommendationPolicy.Evaluate(quantity, CompleteAssessment(), ComparableAssessment(20d), Settings);

        Assert.That(result.Action, Is.EqualTo(expected));
    }

    [Test]
    public void Evaluate_ReturnsUnavailableWhenRequiredDataIsMissing()
    {
        var result = PositionRecommendationPolicy.Evaluate(
            1,
            CompleteAssessment() with { Ytm = null },
            ComparableAssessment(20d),
            Settings);

        Assert.That(result.Action, Is.EqualTo(PositionRecommendationAction.Unavailable));
    }

    [Test]
    public void Evaluate_SellsWhenComparableYtmIsAtLeastHalfAPointHigher()
    {
        var result = PositionRecommendationPolicy.Evaluate(
            1,
            CompleteAssessment() with { Ytm = 17d },
            ComparableAssessment(17.5d),
            Settings);

        Assert.Multiple(() =>
        {
            Assert.That(result.Action, Is.EqualTo(PositionRecommendationAction.Sell));
            Assert.That(result.Reasons, Does.Contain(PositionRecommendationReason.BetterMarketAlternative));
        });
    }

    [Test]
    public void Evaluate_BuysWhenCurrentYtmIsAtLeastHalfAPointHigher()
    {
        var result = PositionRecommendationPolicy.Evaluate(
            1,
            CompleteAssessment() with { Ytm = 18d },
            ComparableAssessment(17.5d),
            Settings);

        Assert.That(result.Action, Is.EqualTo(PositionRecommendationAction.Buy));
    }

    private static BondAssessment CompleteAssessment() => new(
        "SEC", "BOARD", "Текущая", "RUB", "RUB", new DateOnly(2027, 1, 1), 20, CreditRating.AA, 1_000, 2_000,
        true, true, true, null, []);

    private static BondAssessment ComparableAssessment(double ytm) =>
        CompleteAssessment() with { SecId = "ALT", ShortName = "Альтернатива", Ytm = ytm };
}
