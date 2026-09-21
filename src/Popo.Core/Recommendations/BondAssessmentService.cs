using Popo.Core.Bonds;
using Popo.Core.Calculators;
using Popo.Core.Common;
using Popo.Core.HttpClients;

namespace Popo.Core.Recommendations;

public sealed class BondAssessmentService(
    IBondsProvider bondsProvider,
    IRatingsProvider ratingsProvider,
    IMoexHttpClient moexHttpClient,
    IHistoryProvider historyProvider,
    ICashFlowProvider cashFlowProvider) : IBondAssessmentService
{
    public async Task<IReadOnlyList<BondAssessment>> GetAssessmentsAsync(
        InvestmentStrategySettings settings,
        CancellationToken cancellationToken)
    {
        var today = MoscowTime.Today;
        var securities = await bondsProvider.GetSecurities(cancellationToken);
        var ratings = await ratingsProvider.GetBondRatingsAsync(cancellationToken);
        var statistics = await historyProvider.GetDailyVolumeStatistics(cancellationToken);
        var market = (await moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .GroupBy(x => (x.SecId, x.BoardId))
            .ToDictionary(x => x.Key, x => x.First());
        var result = new List<BondAssessment>(securities.Count);

        foreach (var security in securities)
        {
            var issues = new List<BondAssessmentIssue>();
            market.TryGetValue((security.SecId, security.BoardId), out var marketData);
            if (marketData?.CurrentPrice is not { })
                issues.Add(BondAssessmentIssue.MissingMarketPrice);

            var rating = ratings.TryGetValue(security.Isin, out var ratingSource)
                ? CashRecommendationService.GetRecommendationRating(ratingSource)
                : null;
            if (rating is null) issues.Add(BondAssessmentIssue.MissingRating);

            statistics.TryGetValue(security.SecId, out var volume);
            if (volume is null) issues.Add(BondAssessmentIssue.MissingLiquidity);

            var meetsInstrumentType =
                CashRecommendationService.IsAllowedByInstrumentType(security, settings.InstrumentType);
            BondYieldRequest yieldRequest = null!;
            var hasYieldRequest = security.EmitterId is not null &&
                                  !string.IsNullOrWhiteSpace(security.IssuerName) &&
                                  !string.IsNullOrWhiteSpace(security.CurrencyId) &&
                                  CashRecommendationService.TryCreateYieldRequest(security, today, out yieldRequest);
            if (!hasYieldRequest) issues.Add(BondAssessmentIssue.MissingSecurityData);

            double? ytm = null;
            BondCandidate? candidate = null;
            var meetsMaturity = false;
            var meetsOfferWindow = false;
            if (hasYieldRequest && marketData?.CurrentPrice is { } price && rating is { } actualRating &&
                volume is not null)
            {
                var maturityDays = yieldRequest.YieldDateType == BondYieldDateType.Maturity
                    ? yieldRequest.YieldDate.DayNumber - yieldRequest.SettlementDate.DayNumber
                    : security.MatDate!.Value.DayNumber - yieldRequest.SettlementDate.DayNumber;
                meetsMaturity =
                    (settings.MinimumMaturityDays is null || maturityDays >= settings.MinimumMaturityDays) &&
                    (settings.MaximumMaturityDays is null || maturityDays <= settings.MaximumMaturityDays);
                meetsOfferWindow = yieldRequest.YieldDateType != BondYieldDateType.Offer ||
                                   maturityDays >= settings.OfferWindowDays;

                var schedule = await cashFlowProvider.GetYieldSchedule(yieldRequest, cancellationToken);
                if (schedule is null)
                {
                    issues.Add(BondAssessmentIssue.MissingYieldSchedule);
                }
                else
                {
                    var dirtyPrice = BondPriceCalculator.CalculateDirtyPrice(
                        price, schedule.FaceValueInSettlementCurrency, security.AccruedInterest);
                    if (dirtyPrice is null)
                    {
                        issues.Add(BondAssessmentIssue.InvalidYield);
                    }
                    else
                    {
                        ytm = YieldCalculator.TryCalculate(yieldRequest.SettlementDate, dirtyPrice.Value,
                            schedule.CashFlows);
                        if (ytm is null)
                        {
                            issues.Add(BondAssessmentIssue.InvalidYield);
                        }
                        else
                        {
                            candidate = new BondCandidate(
                                security.SecId, security.BoardId, security.EmitterId!.Value, security.IssuerName!,
                                security.CurrencyId!, ytm.Value, dirtyPrice.Value, actualRating,
                                security.MatDate!.Value,
                                security.OfferDate ?? security.BuybackDate,
                                security.IsFloater ? BondCouponType.Floating : BondCouponType.Fixed,
                                volume.Median, schedule.YieldDate, schedule.YieldDateType,
                                CashRecommendationService.CreateCalculationDetails(
                                    yieldRequest.SettlementDate, price, security.AccruedInterest!.Value,
                                    dirtyPrice.Value, schedule));
                        }
                    }
                }
            }

            result.Add(new BondAssessment(
                security.SecId, security.BoardId, security.ShortName, security.CurrencyId, security.FaceUnit,
                candidate?.YieldDate,
                ytm, rating, volume?.Average, volume?.Median,
                meetsMaturity, meetsOfferWindow, meetsInstrumentType, candidate, issues));
        }

        return result;
    }
}

public interface IBondAssessmentService
{
    Task<IReadOnlyList<BondAssessment>> GetAssessmentsAsync(
        InvestmentStrategySettings settings,
        CancellationToken cancellationToken);
}