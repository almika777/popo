using Popo.Core;
using Popo.Core.Bonds;
using Popo.Core.Calculators;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Recommendations;
using NUnit.Framework;

namespace Popo.Tests;

[TestFixture]
public sealed class BondAssessmentServiceFilterTests
{
    [TestCase(399, true)]
    [TestCase(400, true)]
    [TestCase(401, false)]
    public async Task GetAssessments_AppliesMinimumMaturityToMaturityDate(int minimumDays, bool expected)
    {
        var assessment = await AssessAsync(maturityDays: 400,
            settings: Settings() with { MinimumMaturityDays = minimumDays });

        Assert.That(assessment.MeetsMaturityRange, Is.EqualTo(expected));
    }

    [TestCase(399, false)]
    [TestCase(400, true)]
    [TestCase(401, true)]
    public async Task GetAssessments_AppliesMaximumMaturityToMaturityDate(int maximumDays, bool expected)
    {
        var assessment = await AssessAsync(maturityDays: 400,
            settings: Settings() with { MaximumMaturityDays = maximumDays });

        Assert.That(assessment.MeetsMaturityRange, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetAssessments_NullMaturityBoundsAreUnrestricted()
    {
        var assessment = await AssessAsync(maturityDays: 400,
            settings: Settings() with { MinimumMaturityDays = null, MaximumMaturityDays = null });

        Assert.That(assessment.MeetsMaturityRange, Is.True);
    }

    [TestCase(null, true)]
    [TestCase(29, false)]
    [TestCase(30, true)]
    [TestCase(31, true)]
    public async Task GetAssessments_OfferWindowUsesDaysUntilOffer(int? offerDays, bool expected)
    {
        var assessment = await AssessAsync(
            maturityDays: 400,
            offerDays: offerDays,
            settings: Settings() with { OfferWindowDays = 30 });

        Assert.That(assessment.MeetsOfferWindow, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetAssessments_MaturityRangeRemainsIndependentFromOfferDate()
    {
        var assessment = await AssessAsync(
            maturityDays: 400,
            offerDays: 20,
            settings: Settings() with { MinimumMaturityDays = 300, MaximumMaturityDays = 450, OfferWindowDays = 30 });

        Assert.Multiple(() =>
        {
            Assert.That(assessment.MeetsMaturityRange, Is.True);
            Assert.That(assessment.MeetsOfferWindow, Is.False);
        });
    }

    [Test]
    public async Task GetAssessments_CollectsAllMissingInputsWithoutRequestingCashFlows()
    {
        var assessment = await AssessAsync(400,
            transformSecurity: security => security with { SecId = "UNKNOWN", Isin = "UNKNOWN", EmitterId = null },
            transformSchedule: _ => throw new AssertionException("При неполных исходных данных потоки не нужны."));

        Assert.Multiple(() =>
        {
            Assert.That(assessment.Issues, Is.EqualTo(new[]
            {
                BondAssessmentIssue.MissingMarketPrice,
                BondAssessmentIssue.MissingRating,
                BondAssessmentIssue.MissingLiquidity,
                BondAssessmentIssue.MissingSecurityData
            }));
            Assert.That(assessment.Candidate, Is.Null);
            Assert.That(assessment.Ytm, Is.Null);
            Assert.That(assessment.MeetsMaturityRange, Is.False);
            Assert.That(assessment.MeetsOfferWindow, Is.False);
        });
    }

    [Test]
    public async Task GetAssessments_MissingSchedulePreservesIndependentMaturityChecks()
    {
        var assessment = await AssessAsync(400, transformSchedule: _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(assessment.Issues, Is.EqualTo(new[] { BondAssessmentIssue.MissingYieldSchedule }));
            Assert.That(assessment.Candidate, Is.Null);
            Assert.That(assessment.Ytm, Is.Null);
            Assert.That(assessment.MeetsMaturityRange, Is.True);
            Assert.That(assessment.MeetsOfferWindow, Is.True);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAssessments_InvalidPriceOrCashFlowsReturnsInvalidYield(bool missingAccruedInterest)
    {
        var assessment = await AssessAsync(400,
            transformSecurity: security => missingAccruedInterest ? security with { AccruedInterest = null } : security,
            transformSchedule: schedule => missingAccruedInterest ? schedule : schedule with { CashFlows = [] });

        Assert.Multiple(() =>
        {
            Assert.That(assessment.Issues, Is.EqualTo(new[] { BondAssessmentIssue.InvalidYield }));
            Assert.That(assessment.Candidate, Is.Null);
            Assert.That(assessment.Ytm, Is.Null);
            Assert.That(assessment.MeetsMaturityRange, Is.True);
        });
    }

    [Test]
    public async Task GetAssessments_CompleteInputsProduceCandidateWithPriceAndYieldDetails()
    {
        var assessment = await AssessAsync(400);
        var candidate = assessment.Candidate!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(assessment.Issues, Is.Empty);
            Assert.That(assessment.Ytm, Is.EqualTo(candidate.Ytm));
            Assert.That(candidate.Ytm, Is.EqualTo(0d).Within(0.000001d));
            Assert.That(candidate.BuyPrice, Is.EqualTo(1_000d));
            Assert.That(candidate.Rating, Is.EqualTo(CreditRating.AAA));
            Assert.That(candidate.MedianDailyVolume, Is.EqualTo(1_000d));
            Assert.That(candidate.Calculation.MarketPricePercent, Is.EqualTo(100d));
            Assert.That(candidate.Calculation.AccruedInterest, Is.EqualTo(0d));
        });
    }

    private static InvestmentStrategySettings Settings() => new(
        CreditRating.D, null, null, 0d, 0, 100d, 0d);

    private static async Task<BondAssessment> AssessAsync(
        int maturityDays,
        int? offerDays = null,
        InvestmentStrategySettings? settings = null,
        Func<BondSecurityDto, BondSecurityDto>? transformSecurity = null,
        Func<BondYieldSchedule, BondYieldSchedule?>? transformSchedule = null)
    {
        var settleDate = MoscowTime.Today;
        var maturityDate = settleDate.AddDays(maturityDays);
        var security = new BondSecurityDto(
            "SEC", "ISIN", "TQCB", "Корпоративные облигации", "RUB", 1_000d, "RUB", 1,
            "Issuer", 4, settleDate, offerDays is null ? null : settleDate.AddDays(offerDays.Value),
            maturityDate, 0d, BuybackPrice: offerDays is null ? null : 100d);
        if (transformSecurity is not null) security = transformSecurity(security);
        var schedule = new BondYieldSchedule(
            offerDays is null ? maturityDate : settleDate.AddDays(offerDays.Value),
            offerDays is null ? BondYieldDateType.Maturity : BondYieldDateType.Offer,
            1_000d,
            [new CashFlow(1_000d, offerDays is null ? maturityDate : settleDate.AddDays(offerDays.Value))],
            [new BondYieldCashFlow(
                offerDays is null ? maturityDate : settleDate.AddDays(offerDays.Value),
                1_000d,
                BondYieldCashFlowType.Redemption,
                false)]);
        var service = new BondAssessmentService(
            new BondsProvider(security),
            new RatingsProvider(),
            new MarketDataClient(),
            new HistoryProvider(),
            new CashFlowProvider(schedule, transformSchedule));

        return (await service.GetAssessmentsAsync(settings ?? Settings(), CancellationToken.None)).Single();
    }

    private sealed class BondsProvider(BondSecurityDto security) : IBondsProvider
    {
        public Task<IReadOnlyList<BondSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
            IReadOnlyList<BondFaceValueRequest> requests, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<BondSecurityDto>> GetSecuritiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BondSecurityDto>>([security]);
    }

    private sealed class RatingsProvider : IRatingsProvider
    {
        public Task<Dictionary<string, RatingsDto>> GetBondRatingsAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, RatingsDto>
            {
                ["ISIN"] = new("ISIN", CreditRating.AAA, null, null, null)
            });
    }

    private sealed class MarketDataClient : IMoexHttpClient
    {
        public Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexMarketdata>>
            {
                ["SEC"] = [new MoexMarketdata { SecId = "SEC", BoardId = "TQCB", LCurrentPrice = 100d }]
            });
        public Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(string secId, string boardId, DateOnly? from = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MoexIndexHistory>> GetLqdtIndexHistoryAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MoexIndexHistory>> GetLqdtPriceHistoryAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MoexIndexHistory>> GetMoneyMarketFundPriceHistoryAsync(string secId, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class HistoryProvider : IHistoryProvider
    {
        public Task<Dictionary<string, double>> GetMedianDailyVolume(CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<string, DailyVolumeStatistics>> GetDailyVolumeStatistics(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, DailyVolumeStatistics> { ["SEC"] = new(1_000d, 1_000d) });
    }

    private sealed class CashFlowProvider(
        BondYieldSchedule schedule,
        Func<BondYieldSchedule, BondYieldSchedule?>? transformSchedule = null) : ICashFlowProvider
    {
        public Task<BondYieldSchedule?> GetYieldSchedule(BondYieldRequest request, CancellationToken ct) =>
            Task.FromResult(transformSchedule is null ? schedule : transformSchedule(schedule));
    }
}
