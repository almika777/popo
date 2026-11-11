using Popo.Api.Models;
using Popo.Core.Portfolio;

namespace Popo.Api.Services.Position;

public static class OpenPositionTotalsCalculator
{
    public static PositionTotalsResponse Calculate(
        IReadOnlyList<PositionResponse> positions, IReadOnlyList<PortfolioCurrencyRateRecord> rates)
    {
        var ratesByCurrency = rates.ToDictionary(rate => rate.CurrencyId, rate => rate.RateToRub, StringComparer.OrdinalIgnoreCase);
        double value = 0, pnl = 0;
        foreach (var position in positions)
        {
            double rate;
            if (PositionCurrencyRates.IsRubleCurrency(position.CurrencyId))
                rate = 1;
            else if (!ratesByCurrency.TryGetValue(position.CurrencyId, out rate) || !double.IsFinite(rate) || rate <= 0)
                return new PositionTotalsResponse(null, null, null, null);
            value += position.TotalCost * rate;
            pnl += position.UnrealizedPnl * rate;
        }

        var invested = value - pnl;
        return new PositionTotalsResponse(value, invested, pnl, invested == 0 ? null : pnl / invested * 100);
    }
}
