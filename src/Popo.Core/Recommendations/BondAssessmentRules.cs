using Popo.Core.Bonds;

namespace Popo.Core.Recommendations;

/// <summary>Правила оценки облигаций, не зависящие от загрузки данных и отбора рекомендаций.</summary>
internal static class BondAssessmentRules
{
    /// <summary>Проверяет соответствие типа купона выбранному типу инструмента.</summary>
    internal static bool IsAllowedByInstrumentType(BondSecurityDto security, BondInstrumentType instrumentType) =>
        instrumentType switch
        {
            BondInstrumentType.Floating => security.IsFloater,
            BondInstrumentType.Fixed => !security.IsFloater,
            _ => true
        };

    /// <summary>Сохраняет входную цену, НКД и использованные потоки для объяснения расчёта доходности.</summary>
    internal static BondYieldCalculationDetails CreateCalculationDetails(
        DateOnly settlementDate,
        double marketPricePercent,
        double accruedInterest,
        double dirtyPrice,
        BondYieldSchedule schedule) => new(
        settlementDate,
        marketPricePercent,
        dirtyPrice - accruedInterest,
        accruedInterest,
        dirtyPrice,
        schedule.FaceValueInSettlementCurrency,
        schedule.CashFlowDetails);

    /// <summary>Проверяет дату расчётов и выбирает ближайшую будущую оферту либо погашение.</summary>
    internal static bool TryCreateYieldRequest(
        BondSecurityDto security,
        DateOnly today,
        out BondYieldRequest request)
    {
        request = null!;
        if (security.SettleDate is not { } settlementDate ||
            settlementDate < today ||
            security.MatDate is not { } maturityDate ||
            maturityDate <= settlementDate ||
            string.IsNullOrWhiteSpace(security.CurrencyId))
        {
            return false;
        }

        var offerDate = new[] { security.BuybackDate, security.OfferDate }
            .Where(x => x is { } date && date > settlementDate && date < maturityDate)
            .Min();
        var yieldDateType = offerDate is null
            ? BondYieldDateType.Maturity
            : BondYieldDateType.Offer;
        if (yieldDateType == BondYieldDateType.Offer &&
            (security.BuybackPrice is not { } offerPrice ||
             !double.IsFinite(offerPrice) ||
             offerPrice <= 0d))
        {
            return false;
        }

        request = new BondYieldRequest(
            security.SecId,
            security.BoardId,
            security.Isin,
            settlementDate,
            offerDate ?? maturityDate,
            yieldDateType,
            security.FaceValue,
            security.FaceUnit,
            security.CurrencyId,
            yieldDateType == BondYieldDateType.Offer ? security.BuybackPrice : null,
            security.RequiresCouponSchedule,
            security.RequiresExplicitRedemption);
        return true;
    }

    /// <summary>Возвращает наивысший известный рейтинг агентств или null, если рейтингов нет.</summary>
    internal static CreditRating? GetRecommendationRating(RatingsDto ratings)
    {
        var values = new[] { ratings.Acra, ratings.Expert, ratings.Nra, ratings.Nkr }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
        return values.Length == 0 ? null : values.Max();
    }

    /// <summary>Проверяет срок до погашения от даты расчётов независимо от даты оферты.</summary>
    internal static bool MeetsMaturityRange(
        BondSecurityDto security,
        BondYieldRequest request,
        InvestmentStrategySettings settings)
    {
        var maturityDays = security.MatDate!.Value.DayNumber - request.SettlementDate.DayNumber;
        return (settings.MinimumMaturityDays is null || maturityDays >= settings.MinimumMaturityDays) &&
               (settings.MaximumMaturityDays is null || maturityDays <= settings.MaximumMaturityDays);
    }

    /// <summary>Проверяет минимальный срок до оферты; для расчёта до погашения ограничение не применяется.</summary>
    internal static bool MeetsOfferWindow(BondYieldRequest request, InvestmentStrategySettings settings) =>
        request.YieldDateType != BondYieldDateType.Offer ||
        request.YieldDate.DayNumber - request.SettlementDate.DayNumber >= settings.OfferWindowDays;
}
