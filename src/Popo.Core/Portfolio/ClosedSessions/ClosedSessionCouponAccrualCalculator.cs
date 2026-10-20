namespace Popo.Core.Portfolio.ClosedSessions;

/// <summary>Линейное распределение суммы каждого купонного периода по дням владения.</summary>
public sealed class ClosedSessionCouponAccrualCalculator
{
    /// <summary>Начисляет купоны по дням владения; сообщает о неполном графике, сумме купона или валютном курсе.</summary>
    public ClosedSessionIncome Calculate(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings, bool preferRubValue = false,
        bool carryForwardMissingFloaterCoupon = false)
    {
        decimal total = 0;
        var periods = instrument.Coupons.Distinct().ToArray();
        foreach (var holding in holdings)
        {
            for (var date = holding.From; date < holding.To; date = date.AddDays(1))
            {
                var matching = periods.Where(x => x.StartDate <= date && date < x.EndDate).ToArray();
                if (matching.Length == 0 && instrument.CouponFrequency == 0 && periods.Length == 0)
                    continue;
                if (matching.Length != 1)
                    return new ClosedSessionIncome(null,
                        $"Купонный график отсутствует или неоднозначен на {date:yyyy-MM-dd}.");

                var period = matching[0];
                var couponPeriod = period;
                if (carryForwardMissingFloaterCoupon && !HasCouponAmount(period))
                {
                    var earlierCoupons = periods
                        .Where(x => x.EndDate <= period.StartDate && HasCouponAmount(x))
                        .OrderByDescending(x => x.EndDate)
                        .ToArray();
                    if (earlierCoupons.Length > 0)
                    {
                        var latestEndDate = earlierCoupons[0].EndDate;
                        var latestCoupons = earlierCoupons.Where(x => x.EndDate == latestEndDate).ToArray();
                        if (latestCoupons.Length != 1)
                            return new ClosedSessionIncome(null,
                                $"Купонный график отсутствует или неоднозначен на {date:yyyy-MM-dd}.");
                        couponPeriod = latestCoupons[0];
                    }
                }
                var amount = ClosedSessionCurrencyConverter.Convert(
                    preferRubValue && ClosedSessionCurrencyConverter.IsRuble(instrument.CurrencyId)
                        && couponPeriod.ValueInRub is >= 0 ? null : couponPeriod.Value,
                    couponPeriod.ValueInRub, couponPeriod.FaceUnit, instrument.CurrencyId, date,
                    instrument.CurrencyRates);
                if (!amount.HasValue)
                    return new ClosedSessionIncome(null,
                        $"Неизвестна сумма купона или валютный курс для периода {period.StartDate:yyyy-MM-dd} — {period.EndDate:yyyy-MM-dd}.");
                total += holding.Quantity * amount.Value / (period.EndDate.DayNumber - period.StartDate.DayNumber);
            }
        }
        return new ClosedSessionIncome(total);
    }

    private static bool HasCouponAmount(ClosedSessionCoupon period)
        => period.Value is >= 0 || period.ValueInRub is >= 0;
}
