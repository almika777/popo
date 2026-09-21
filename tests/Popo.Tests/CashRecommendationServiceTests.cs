using NUnit.Framework;
using Popo.Core.Bonds;
using Popo.Core.Calculators;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class CashRecommendationServiceTests
{
    [Test]
    public void GetRecommendationRating_UsesTheHighestAvailableAgencyRating()
    {
        var ratings = new RatingsDto(
            "RU000A107GX8",
            CreditRating.AA_minus,
            CreditRating.A_plus,
            null,
            null);

        Assert.That(CashRecommendationService.GetRecommendationRating(ratings), Is.EqualTo(CreditRating.AA_minus));
    }

    [Test]
    public void GetRecommendationRating_ReturnsNullWhenNoAgencyRatingExists()
    {
        Assert.That(
            CashRecommendationService.GetRecommendationRating(new RatingsDto("RU000A107GX8", null, null, null, null)),
            Is.Null);
    }

    [Test]
    public void CreateCalculationDetails_PreservesTheExactPriceInputsAndYieldCashFlows()
    {
        var settlementDate = new DateOnly(2026, 8, 20);
        var yieldDate = new DateOnly(2026, 12, 14);
        var flow = new BondYieldCashFlow(yieldDate, 1_089.22d, BondYieldCashFlowType.Redemption, false);
        var schedule = new BondYieldSchedule(
            yieldDate,
            BondYieldDateType.Maturity,
            1_000d,
            [new CashFlow(flow.Amount, flow.Date)],
            [flow]);

        var details = CashRecommendationService.CreateCalculationDetails(
            settlementDate,
            100.5d,
            30.25d,
            1_035.25d,
            schedule);

        Assert.Multiple(() =>
        {
            Assert.That(details.SettlementDate, Is.EqualTo(settlementDate));
            Assert.That(details.MarketPricePercent, Is.EqualTo(100.5d));
            Assert.That(details.CleanPrice, Is.EqualTo(1_005d));
            Assert.That(details.AccruedInterest, Is.EqualTo(30.25d));
            Assert.That(details.DirtyPrice, Is.EqualTo(1_035.25d));
            Assert.That(details.FaceValue, Is.EqualTo(1_000d));
            Assert.That(details.CashFlows, Is.SameAs(schedule.CashFlowDetails));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void TryCreateYieldRequest_PreservesMoexSettlementDate(int settlementLagDays)
    {
        var today = new DateOnly(2026, 8, 19);
        var settlementDate = today.AddDays(settlementLagDays);
        var security = Security(settlementDate: settlementDate);

        var success = CashRecommendationService.TryCreateYieldRequest(
            security,
            today,
            out var request);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(request.SettlementDate, Is.EqualTo(settlementDate));
        });
    }

    [Test]
    public void TryCreateYieldRequest_RejectsMissingOrStaleSettlementDate()
    {
        var today = new DateOnly(2026, 8, 19);

        Assert.Multiple(() =>
        {
            Assert.That(CashRecommendationService.TryCreateYieldRequest(
                Security(settlementDate: null), today, out _), Is.False);
            Assert.That(CashRecommendationService.TryCreateYieldRequest(
                Security(settlementDate: today.AddDays(-1)), today, out _), Is.False);
        });
    }

    [Test]
    public void TryCreateYieldRequest_TargetsFutureOfferAndCarriesOfferPrice()
    {
        var today = new DateOnly(2026, 8, 19);
        var offerDate = new DateOnly(2027, 2, 19);
        var security = Security(
            settlementDate: today.AddDays(1),
            buybackDate: offerDate,
            buybackPrice: 98.5d);

        var success = CashRecommendationService.TryCreateYieldRequest(
            security,
            today,
            out var request);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(request.YieldDate, Is.EqualTo(offerDate));
            Assert.That(request.YieldDateType, Is.EqualTo(BondYieldDateType.Offer));
            Assert.That(request.OfferPricePercent, Is.EqualTo(98.5d));
        });
    }

    [Test]
    public void TryCreateYieldRequest_DoesNotApplyBuybackPriceToUnrelatedOptionDate()
    {
        var today = new DateOnly(2026, 8, 19);
        var buybackDate = new DateOnly(2027, 6, 19);
        var security = Security(
            settlementDate: today.AddDays(1),
            buybackDate: buybackDate,
            buybackPrice: 98.5d) with
        {
            PutOptionDate = new DateOnly(2027, 2, 19)
        };

        var success = CashRecommendationService.TryCreateYieldRequest(
            security,
            today,
            out var request);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(request.YieldDate, Is.EqualTo(buybackDate));
            Assert.That(request.OfferPricePercent, Is.EqualTo(98.5d));
        });
    }

    [Test]
    public void TryCreateYieldRequest_RejectsFutureOfferWithoutPrice()
    {
        var today = new DateOnly(2026, 8, 19);
        var security = Security(
            settlementDate: today.AddDays(1),
            buybackDate: new DateOnly(2027, 2, 19),
            buybackPrice: null);

        var success = CashRecommendationService.TryCreateYieldRequest(
            security,
            today,
            out _);

        Assert.That(success, Is.False);
    }

    [Test]
    public void TryCreateYieldRequest_TargetsMaturityWhenOfferIsNotFuture()
    {
        var today = new DateOnly(2026, 8, 19);
        var maturityDate = new DateOnly(2027, 8, 20);
        var security = Security(
            settlementDate: today.AddDays(1),
            maturityDate: maturityDate,
            buybackDate: today,
            buybackPrice: 100d);

        var success = CashRecommendationService.TryCreateYieldRequest(
            security,
            today,
            out var request);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(request.YieldDate, Is.EqualTo(maturityDate));
            Assert.That(request.YieldDateType, Is.EqualTo(BondYieldDateType.Maturity));
            Assert.That(request.OfferPricePercent, Is.Null);
        });
    }

    [Test]
    public void TryCreateYieldRequest_RejectsPerpetualBond()
    {
        var today = new DateOnly(2026, 8, 19);

        var success = CashRecommendationService.TryCreateYieldRequest(
            Security(settlementDate: today.AddDays(1), perpetual: true),
            today,
            out _);

        Assert.That(success, Is.False);
    }

    [TestCase("Дисконтные облигации", false, false)]
    [TestCase("Структурные облигации", true, true)]
    [TestCase("Конвертируемые облигации", true, true)]
    [TestCase("Корпоративные облигации", true, false)]
    public void TryCreateYieldRequest_ClassifiesRequiredContractData(
        string bondType,
        bool requiresCouponSchedule,
        bool requiresExplicitRedemption)
    {
        var today = new DateOnly(2026, 8, 19);

        var success = CashRecommendationService.TryCreateYieldRequest(
            Security(settlementDate: today.AddDays(1), bondType: bondType),
            today,
            out var request);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(request.RequiresCouponSchedule, Is.EqualTo(requiresCouponSchedule));
            Assert.That(request.RequiresExplicitRedemption, Is.EqualTo(requiresExplicitRedemption));
        });
    }

    [Test]
    public void IsAllowedByInstrumentType_SelectsInstrumentType()
    {
        var floater = Security(
            settlementDate: new DateOnly(2026, 8, 20),
            bondType: "Флоатерные облигации");
        var moexFloater = Security(
            settlementDate: new DateOnly(2026, 8, 20),
            bondType: "Облигация с плавающим купоном");
        var fixedBond = Security(
            settlementDate: new DateOnly(2026, 8, 20),
            bondType: "Корпоративные облигации");

        Assert.Multiple(() =>
        {
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(floater, BondInstrumentType.Any), Is.True);
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(fixedBond, BondInstrumentType.Any), Is.True);
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(floater, BondInstrumentType.Floating), Is.True);
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(moexFloater, BondInstrumentType.Floating), Is.True);
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(fixedBond, BondInstrumentType.Floating), Is.False);
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(floater, BondInstrumentType.Fixed), Is.False);
            Assert.That(CashRecommendationService.IsAllowedByInstrumentType(fixedBond, BondInstrumentType.Fixed), Is.True);
        });
    }

    [Test]
    public void IsAllowedByCurrencies_AppliesFaceUnitAndTradingCurrencyIndependently()
    {
        var replacementBond = Security(
            settlementDate: new DateOnly(2026, 8, 20),
            faceUnit: "USD",
            currencyId: "RUB");

        Assert.Multiple(() =>
        {
            Assert.That(CashRecommendationService.IsAllowedByCurrencies(replacementBond, null, null), Is.True);
            Assert.That(CashRecommendationService.IsAllowedByCurrencies(replacementBond, "USD", "RUB"), Is.True);
            Assert.That(CashRecommendationService.IsAllowedByCurrencies(replacementBond, "RUB", "RUB"), Is.False);
            Assert.That(CashRecommendationService.IsAllowedByCurrencies(replacementBond, "USD", "USD"), Is.False);
        });
    }

    private static BondSecurityDto Security(
        DateOnly? settlementDate,
        DateOnly? maturityDate = null,
        DateOnly? buybackDate = null,
        double? buybackPrice = null,
        bool perpetual = false,
        string bondType = "Корпоративные облигации",
        string faceUnit = "RUB",
        string currencyId = "RUB") => new(
        "SEC",
        "ISIN",
        "TQCB",
        bondType,
        faceUnit,
        1_000d,
        currencyId,
        1,
        "Эмитент",
        4,
        settlementDate,
        buybackDate,
        perpetual ? null : maturityDate ?? new DateOnly(2027, 8, 20),
        10d,
        BuybackDate: buybackDate,
        BuybackPrice: buybackPrice);
}
