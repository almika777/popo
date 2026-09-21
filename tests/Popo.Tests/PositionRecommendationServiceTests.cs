using NUnit.Framework;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class PositionRecommendationServiceTests
{
    [Test]
    public void FindBestComparable_IgnoresBondWithDifferentCouponType()
    {
        var current = Assessment("CURRENT", 18, BondCouponType.Fixed);
        var floating = Assessment("FLOATING", 25, BondCouponType.Floating);
        var fixedBond = Assessment("FIXED", 19, BondCouponType.Fixed);

        var result = PositionRecommendationService.FindBestComparable(
            current,
            [floating, fixedBond],
            InvestmentStrategySettings.Defaults);

        Assert.That(result?.SecId, Is.EqualTo("FIXED"));
    }

    [Test]
    public void Evaluate_ExposesCleanCalculationPrices()
    {
        var current = Assessment("CURRENT", 18, BondCouponType.Fixed, 606.5);
        var alternative = Assessment("ALTERNATIVE", 19, BondCouponType.Fixed, 1_002.3);

        var result = PositionRecommendationPolicy.Evaluate(
            1, current, alternative, InvestmentStrategySettings.Defaults);

        Assert.Multiple(() =>
        {
            Assert.That(result.CalculationPrice, Is.EqualTo(606.5));
            Assert.That(result.Alternative?.CalculationPrice, Is.EqualTo(1_002.3));
        });
    }

    private static BondAssessment Assessment(
        string secId,
        double ytm,
        BondCouponType couponType,
        double calculationPrice = 1_000)
    {
        var yieldDate = new DateOnly(2027, 1, 1);
        var calculation = new BondYieldCalculationDetails(
            new DateOnly(2026, 8, 26), 100, calculationPrice, 0, calculationPrice, 1_000, []);
        var candidate = new BondCandidate(
            secId, "TQCB", 1, "Эмитент", "RUB", ytm, 1_000, CreditRating.AA,
            yieldDate, null, couponType, 10_000, yieldDate, BondYieldDateType.Maturity, calculation);

        return new BondAssessment(
            secId, "TQCB", secId, "RUB", "RUB", yieldDate, ytm, CreditRating.AA,
            10_000, 10_000, true, true, true, candidate, []);
    }
}
