using Microsoft.EntityFrameworkCore;
using Popo.Core;
using Popo.Core.Recommendations;
using Popo.Storage.Entities;
using Popo.Storage.Entities.Moex;

namespace Popo.Storage.Providers;

public class CashFlowProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : ICashFlowProvider
{
    public async Task<BondYieldSchedule?> GetYieldSchedule(
        BondYieldRequest request,
        CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var coupons = await db.MoexCouponsEntities
            .AsNoTracking()
            .Where(x => x.SecId == request.SecId && x.Isin == request.Isin)
            .OrderBy(x => x.CouponDate)
            .ToListAsync(cancellationToken: ct);
        var amortizations = await db.MoexAmortsEntities
            .AsNoTracking()
            .Where(x => x.SecId == request.SecId && x.Isin == request.Isin)
            .OrderBy(x => x.AmortDate)
            .ToListAsync(cancellationToken: ct);

        var rateDate = await db.CurrencyRates
            .AsNoTracking()
            .Where(x => x.RateDate <= request.SettlementDate)
            .MaxAsync(x => (DateOnly?)x.RateDate, cancellationToken: ct);
        var rates = rateDate is null
            ? new Dictionary<string, CurrencyRateEntity>(StringComparer.OrdinalIgnoreCase)
            : await db.CurrencyRates
                .AsNoTracking()
                .Where(x => x.RateDate == rateDate.Value)
                .ToDictionaryAsync(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase, ct);

        if (!TryGetCurrencyConversionFactor(
                request.FaceUnit,
                request.SettlementCurrency,
                rates,
                out var faceFactor))
        {
            return null;
        }

        var couponSources = new List<BondCouponSource>(coupons.Count);
        foreach (var coupon in coupons)
        {
            if (!TryGetCurrencyConversionFactor(
                    coupon.FaceUnit,
                    request.SettlementCurrency,
                    rates,
                    out var factor))
            {
                return null;
            }

            var sourceFaceValue = coupon.FaceValue > 0d
                ? coupon.FaceValue
                : coupon.InitialFaceValue;
            couponSources.Add(new BondCouponSource(
                coupon.CouponDate,
                coupon.StartDate,
                coupon.Value * factor,
                sourceFaceValue * factor,
                coupon.ValuePercent));
        }

        var principalSources = new List<BondPrincipalSource>(amortizations.Count);
        foreach (var amortization in amortizations)
        {
            if (!TryGetCurrencyConversionFactor(
                    amortization.FaceUnit,
                    request.SettlementCurrency,
                    rates,
                    out var factor))
            {
                return null;
            }

            principalSources.Add(new BondPrincipalSource(
                amortization.AmortDate,
                amortization.Value * factor,
                amortization.ValuePercent,
                amortization.InitialFaceValue * factor));
        }

        var convertedRequest = request with
        {
            SettlementFaceValue = request.SettlementFaceValue * faceFactor
        };

        return BondYieldCashFlowBuilder.TryBuild(
            convertedRequest,
            couponSources,
            principalSources);
    }

    internal static void CompleteFutureCoupons(
        List<MoexCouponsEntity> coupons,
        IReadOnlyList<MoexAmortsEntity> amorts,
        DateOnly today,
        int? couponFrequency = null)
    {
        double? lastKnownPercent = null;

        foreach (var coupon in coupons.OrderBy(x => x.CouponDate))
        {
            var faceValue = GetFaceValueBeforeAmortization(coupon, amorts);
            var couponPeriodFraction = GetCouponPeriodFraction(coupon, couponFrequency);

            if (coupon.Value is not null)
            {
                if (faceValue > 0)
                    lastKnownPercent = coupon.Value.Value / faceValue * 100d / couponPeriodFraction;

                continue;
            }

            if (coupon.ValuePercent is not null)
            {
                lastKnownPercent = coupon.ValuePercent;

                if (coupon.CouponDate > today)
                    coupon.Value = faceValue * coupon.ValuePercent.Value / 100d * couponPeriodFraction;

                continue;
            }

            if (coupon.CouponDate <= today || lastKnownPercent is null)
                continue;

            coupon.ValuePercent = lastKnownPercent;
            coupon.Value = faceValue * lastKnownPercent.Value / 100d * couponPeriodFraction;
        }
    }

    internal static List<CashFlow> ConvertFutureCouponsToRub(
        IEnumerable<MoexCouponsEntity> coupons,
        IReadOnlyDictionary<string, CurrencyRateEntity> rates,
        DateOnly today)
    {
        var cashFlows = new List<CashFlow>();

        foreach (var coupon in coupons
                     .Where(x => x.CouponDate > today && x.Value is not null)
                     .OrderBy(x => x.CouponDate))
        {
            if (!TryGetCurrencyRate(coupon.FaceUnit, rates, out var unitRate))
                continue;

            cashFlows.Add(new CashFlow(coupon.Value!.Value * unitRate, coupon.CouponDate));
        }

        return cashFlows;
    }

    internal static List<CashFlow> ConvertFutureCashFlowsToRub(
        IEnumerable<MoexCouponsEntity> coupons,
        IReadOnlyList<MoexAmortsEntity> amorts,
        IReadOnlyDictionary<string, CurrencyRateEntity> rates,
        DateOnly today)
    {
        var cashFlows = ConvertFutureCouponsToRub(coupons, rates, today);
        var futureAmortizations = amorts
            .Where(x => x.AmortDate > today)
            .OrderBy(x => x.AmortDate);

        foreach (var amortization in futureAmortizations)
        {
            var amortizationValue = GetAmortizationValue(amortization);
            if (amortizationValue is null ||
                !TryGetCurrencyRate(amortization.FaceUnit, rates, out var unitRate))
            {
                continue;
            }

            cashFlows.Add(new CashFlow(
                amortizationValue.Value * unitRate,
                amortization.AmortDate));
        }

        return cashFlows.OrderBy(x => x.Date).ToList();
    }

    private static double GetFaceValueBeforeAmortization(
        MoexCouponsEntity coupon,
        IReadOnlyList<MoexAmortsEntity> amorts)
    {
        var initialFaceValue = coupon.InitialFaceValue > 0
            ? coupon.InitialFaceValue
            : coupon.FaceValue;

        var amortizedValue = amorts
            .Where(x => x.SecId == coupon.SecId && x.Isin == coupon.Isin && x.AmortDate < coupon.CouponDate)
            .Sum(x => x.Value ?? (x.ValuePercent is not null
                ? x.InitialFaceValue * x.ValuePercent.Value / 100d
                : 0d));

        return Math.Max(0d, initialFaceValue - amortizedValue);
    }

    private static double GetCouponPeriodFraction(MoexCouponsEntity coupon, int? couponFrequency)
    {
        var periodDays = coupon.CouponDate.DayNumber - coupon.StartDate.DayNumber;
        if (periodDays > 0)
            return periodDays / 365d;

        return couponFrequency is > 0
            ? 1d / couponFrequency.Value
            : 1d;
    }

    private static double? GetAmortizationValue(MoexAmortsEntity amortization)
    {
        if (amortization.Value is not null)
            return amortization.Value;

        if (amortization.ValuePercent is not null)
        {
            var initialFaceValue = amortization.InitialFaceValue > 0
                ? amortization.InitialFaceValue
                : amortization.FaceValue;

            return initialFaceValue * amortization.ValuePercent.Value / 100d;
        }

        return null;
    }

    private static bool TryGetCurrencyRate(
        string currency,
        IReadOnlyDictionary<string, CurrencyRateEntity> rates,
        out double unitRate)
    {
        if (string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase)
            || string.Equals(currency, "SUR", StringComparison.OrdinalIgnoreCase))
        {
            unitRate = 1d;
            return true;
        }

        var rate = rates.FirstOrDefault(x =>
            string.Equals(x.Key, currency, StringComparison.OrdinalIgnoreCase)).Value;
        if (rate is null || !double.IsFinite(rate.UnitRate) || rate.UnitRate <= 0d)
        {
            unitRate = 0d;
            return false;
        }

        unitRate = rate.UnitRate;
        return true;
    }

    internal static bool TryGetCurrencyConversionFactor(
        string sourceCurrency,
        string settlementCurrency,
        IReadOnlyDictionary<string, CurrencyRateEntity> rates,
        out double factor)
    {
        if (!TryGetRubRate(sourceCurrency, rates, out var sourceRubRate) ||
            !TryGetRubRate(settlementCurrency, rates, out var settlementRubRate))
        {
            factor = 0d;
            return false;
        }

        factor = sourceRubRate / settlementRubRate;
        return double.IsFinite(factor) && factor > 0d;
    }

    private static bool TryGetRubRate(
        string currency,
        IReadOnlyDictionary<string, CurrencyRateEntity> rates,
        out double rubRate)
    {
        if (NormalizeCurrency(currency) == "RUB")
        {
            rubRate = 1d;
            return true;
        }

        var rate = rates.FirstOrDefault(x =>
            NormalizeCurrency(x.Key) == NormalizeCurrency(currency)).Value;
        if (rate is null || !double.IsFinite(rate.UnitRate) || rate.UnitRate <= 0d)
        {
            rubRate = 0d;
            return false;
        }

        rubRate = rate.UnitRate;
        return true;
    }

    private static string NormalizeCurrency(string currency) =>
        currency.Trim().ToUpperInvariant() switch
        {
            "SUR" or "RUR" => "RUB",
            var normalized => normalized
        };
}
