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

        Assert.That(BondAssessmentRules.GetRecommendationRating(ratings), Is.EqualTo(CreditRating.AA_minus));
    }

    [TestCase(CreditRating.AA_minus, null, CreditRating.A_plus, null, CreditRating.AA_minus)]
    [TestCase(null, CreditRating.BBB_minus, null, CreditRating.A, CreditRating.A)]
    [TestCase(CreditRating.D, null, CreditRating.AAA, null, CreditRating.AAA)]
    public void GetRecommendationRating_IgnoresMissingAgenciesAndUsesBestKnownRating(
        CreditRating? acra,
        CreditRating? expert,
        CreditRating? nra,
        CreditRating? nkr,
        CreditRating expected)
    {
        var ratings = new RatingsDto("RU000A107GX8", acra, expert, nra, nkr);

        Assert.That(BondAssessmentRules.GetRecommendationRating(ratings), Is.EqualTo(expected));
    }

    [Test]
    public void GetRecommendationRating_ReturnsNullWhenNoAgencyRatingExists()
    {
        Assert.That(
            BondAssessmentRules.GetRecommendationRating(new RatingsDto("RU000A107GX8", null, null, null, null)),
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

        var details = BondAssessmentRules.CreateCalculationDetails(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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
            Assert.That(BondAssessmentRules.TryCreateYieldRequest(
                Security(settlementDate: null), today, out _), Is.False);
            Assert.That(BondAssessmentRules.TryCreateYieldRequest(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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

        var success = BondAssessmentRules.TryCreateYieldRequest(
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
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(floater, BondInstrumentType.Any), Is.True);
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(fixedBond, BondInstrumentType.Any), Is.True);
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(floater, BondInstrumentType.Floating), Is.True);
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(moexFloater, BondInstrumentType.Floating), Is.True);
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(fixedBond, BondInstrumentType.Floating), Is.False);
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(floater, BondInstrumentType.Fixed), Is.False);
            Assert.That(BondAssessmentRules.IsAllowedByInstrumentType(fixedBond, BondInstrumentType.Fixed), Is.True);
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

    [TestCaseSource(nameof(MinimumRatingCases))]
    public async Task GetRecommendations_AppliesMinimumRatingIndependently(
        CreditRating minimumRating,
        CreditRating candidateRating,
        bool expected)
    {
        var assessment = Assessment(rating: candidateRating);
        var settings = Settings() with { MinimumRating = minimumRating };
        var service = RecommendationService(assessment, settings);

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Has.Count.EqualTo(expected ? 1 : 0));
    }

    [TestCase(1_000d, 999.99d, false)]
    [TestCase(1_000d, 1_000d, true)]
    [TestCase(1_000d, 1_000.01d, true)]
    public async Task GetRecommendations_AppliesMinimumMedianVolumeIndependently(
        double minimum,
        double median,
        bool expected)
    {
        var service = RecommendationService(Assessment(medianDailyVolume: median), Settings() with { MinimumMedianDailyVolume = minimum });

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Has.Count.EqualTo(expected ? 1 : 0));
    }

    [Test]
    public async Task GetRecommendations_ExcludesMissingMedianVolumeStatistics()
    {
        var service = RecommendationService(Assessment(medianDailyVolume: null), Settings());

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Is.Empty);
    }

    [Test]
    public async Task GetRecommendations_ExcludesCandidatesOutsideMaturityRange()
    {
        var service = RecommendationService(Assessment(meetsMaturityRange: false), Settings());

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Is.Empty);
    }

    [Test]
    public async Task GetRecommendations_ExcludesCandidatesOutsideOfferWindow()
    {
        var service = RecommendationService(Assessment(meetsOfferWindow: false), Settings());

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Is.Empty);
    }

    [TestCase(15d, 14.99d, false)]
    [TestCase(15d, 15d, true)]
    [TestCase(15d, 15.01d, true)]
    public async Task GetRecommendations_AppliesMinimumYtmIndependently(double minimum, double ytm, bool expected)
    {
        var service = RecommendationService(Assessment(ytm: ytm), Settings() with { MinimumYtm = minimum });

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Has.Count.EqualTo(expected ? 1 : 0));
    }

    [TestCase(30d, 29.99d, true)]
    [TestCase(30d, 30d, true)]
    [TestCase(30d, 30.01d, false)]
    public async Task GetRecommendations_AppliesMaximumYtmIndependently(double maximum, double ytm, bool expected)
    {
        var service = RecommendationService(Assessment(ytm: ytm), Settings() with { MaximumYtm = maximum });

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Has.Count.EqualTo(expected ? 1 : 0));
    }

    [TestCase(BondInstrumentType.Any, true)]
    [TestCase(BondInstrumentType.Floating, true)]
    [TestCase(BondInstrumentType.Fixed, false)]
    public async Task GetRecommendations_AppliesInstrumentTypeIndependently(
        BondInstrumentType instrumentType,
        bool expected)
    {
        var service = RecommendationService(Assessment(meetsInstrumentType: expected), Settings() with { InstrumentType = instrumentType });

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Has.Count.EqualTo(expected ? 1 : 0));
    }

    [TestCase("USD", null, "usd", "RUB", true)]
    [TestCase("USD", null, "RUB", "RUB", false)]
    [TestCase(null, "RUB", "USD", "rub", true)]
    [TestCase(null, "RUB", "USD", "USD", false)]
    [TestCase(null, null, "USD", "RUB", true)]
    [TestCase("USD", "RUB", "usd", "rub", true)]
    [TestCase("USD", "RUB", "RUB", "RUB", false)]
    [TestCase("USD", "RUB", "USD", "USD", false)]
    public async Task GetRecommendations_AppliesFaceUnitAndCurrencyIndependently(
        string? faceUnit,
        string? currencyId,
        string actualFaceUnit,
        string actualCurrencyId,
        bool expected)
    {
        var service = RecommendationService(Assessment(faceUnit: actualFaceUnit, currencyId: actualCurrencyId), Settings() with
        {
            FaceUnit = faceUnit,
            CurrencyId = currencyId
        });

        var recommendations = await service.GetRecommendations();

        Assert.That(recommendations, Has.Count.EqualTo(expected ? 1 : 0));
    }

    private static IEnumerable<TestCaseData> MinimumRatingCases()
    {
        foreach (var minimum in Enum.GetValues<CreditRating>())
        foreach (var candidate in Enum.GetValues<CreditRating>())
            yield return new TestCaseData(minimum, candidate, candidate >= minimum)
                .SetName($"MinimumRating_{minimum}_Candidate_{candidate}_Expected_{candidate >= minimum}");
    }

    private static InvestmentStrategySettings Settings() => new(
        CreditRating.D, null, null, 0d, 0, 100d, 0d);

    private static BondAssessment Assessment(
        CreditRating rating = CreditRating.AAA,
        double? ytm = 20d,
        double? medianDailyVolume = 1_000d,
        bool meetsMaturityRange = true,
        bool meetsOfferWindow = true,
        bool meetsInstrumentType = true,
        string? faceUnit = "RUB",
        string? currencyId = "RUB")
    {
        var candidate = new BondCandidate(
            "SEC", "TQCB", 1, "Issuer", currencyId ?? "RUB", ytm ?? 20d, 100d,
            rating, new DateOnly(2027, 1, 1), null, BondCouponType.Fixed,
            medianDailyVolume ?? 1_000d, new DateOnly(2027, 1, 1), BondYieldDateType.Maturity,
            new BondYieldCalculationDetails(new DateOnly(2026, 1, 1), 100d, 1_000d, 0d, 1_000d, 1_000d, []));
        return new BondAssessment(
            "SEC", "TQCB", "Name", currencyId, faceUnit, candidate.YieldDate,
            ytm, rating, medianDailyVolume, medianDailyVolume,
            meetsMaturityRange, meetsOfferWindow, meetsInstrumentType, candidate, []);
    }

    private static FakeCashRecommendationService RecommendationService(
        BondAssessment assessment,
        InvestmentStrategySettings settings) => new(assessment, settings);

    private sealed class FakeCashRecommendationService(BondAssessment assessment, InvestmentStrategySettings settings)
        : CashRecommendationService(new SettingsStore(settings), new AssessmentService(assessment))
    {
        public Task<List<BondCandidate>> GetRecommendations() => base.GetRecommendations(CancellationToken.None);
    }

    private sealed class SettingsStore(InvestmentStrategySettings? settings = null)
        : ICashInvestmentRecommendationStore
    {
        public Task<InvestmentStrategySettings> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(settings ?? Settings());

        public Task<IReadOnlyList<InvestmentStrategyPreset>> GetPresetsAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<InvestmentStrategyPreset?> GetPresetAsync(int id, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<InvestmentStrategyPreset> SavePresetAsync(InvestmentStrategyPreset preset, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<bool> DeletePresetAsync(int id, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<bool> ActivatePresetAsync(int id, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<bool> SaveSettingsAsync(InvestmentStrategySettings value, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class AssessmentService(BondAssessment assessment) : IBondAssessmentService
    {
        public Task<IReadOnlyList<BondAssessment>> GetAssessmentsAsync(
            InvestmentStrategySettings settings,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BondAssessment>>([assessment]);
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
