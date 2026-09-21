using NUnit.Framework;
using Popo.Api.Models;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class CashRecommendationApiMapperTests
{
    [Test]
    public void ToResponse_ExposesYieldTargetDateAndType()
    {
        var yieldDate = new DateOnly(2027, 2, 19);
        var candidate = new BondCandidate(
            "SEC",
            "TQCB",
            1,
            "Эмитент",
            "RUB",
            17.25d,
            1_012.34d,
            CreditRating.AAA,
            new DateOnly(2028, 8, 19),
            yieldDate,
            BondCouponType.Floating,
            1_000d,
            yieldDate,
            BondYieldDateType.Offer,
            new BondYieldCalculationDetails(
                new DateOnly(2026, 8, 20),
                100.5d,
                1_005d,
                30.25d,
                1_035.25d,
                1_000d,
                [new BondYieldCashFlow(yieldDate, 1_080d, BondYieldCashFlowType.Redemption, false)]));

        var response = CashRecommendationApiMapper.ToResponse([candidate]);

        Assert.Multiple(() =>
        {
            Assert.That(response.Bonds[0].YieldDate, Is.EqualTo(yieldDate));
            Assert.That(response.Bonds[0].YieldDateType, Is.EqualTo(BondYieldDateType.Offer));
            Assert.That(response.Bonds[0].Calculation.SettlementDate, Is.EqualTo(new DateOnly(2026, 8, 20)));
            Assert.That(response.Bonds[0].Calculation.MarketPricePercent, Is.EqualTo(100.5d));
            Assert.That(response.Bonds[0].Calculation.CleanPrice, Is.EqualTo(1_005d));
            Assert.That(response.Bonds[0].Calculation.AccruedInterest, Is.EqualTo(30.25d));
            Assert.That(response.Bonds[0].Calculation.DirtyPrice, Is.EqualTo(1_035.25d));
            Assert.That(response.Bonds[0].Calculation.FaceValue, Is.EqualTo(1_000d));
            Assert.That(response.Bonds[0].Calculation.CashFlows, Has.Count.EqualTo(1));
            Assert.That(response.Bonds[0].Calculation.CashFlows[0].Type, Is.EqualTo(BondYieldCashFlowType.Redemption));
        });
    }

    [Test]
    public void ToResponse_ExposesInstrumentTypeFromSettings()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                CashInvestmentRecommendationApiMapper.ToResponse(
                    InvestmentStrategySettings.Defaults with { InstrumentType = BondInstrumentType.Floating })
                    .InstrumentType,
                Is.EqualTo(BondInstrumentType.Floating));
            Assert.That(
                CashInvestmentRecommendationApiMapper.ToResponse(InvestmentStrategySettings.Defaults).InstrumentType,
                Is.EqualTo(BondInstrumentType.Any));
        });
    }

    [Test]
    public void ToResponse_ExposesNominalAndTradingCurrencyFiltersFromSettings()
    {
        var response = CashInvestmentRecommendationApiMapper.ToResponse(
            InvestmentStrategySettings.Defaults with { FaceUnit = "USD", CurrencyId = "RUB" });

        Assert.Multiple(() =>
        {
            Assert.That(response.FaceUnit, Is.EqualTo("USD"));
            Assert.That(response.CurrencyId, Is.EqualTo("RUB"));
        });
    }
}
