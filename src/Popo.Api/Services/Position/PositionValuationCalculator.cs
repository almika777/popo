using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using static Popo.Api.Services.Position.PositionCurrencyRates;

namespace Popo.Api.Services.Position;

internal static class PositionValuationCalculator
{
    internal static PositionMarketPriceData CalculateMarketPrice(
        PositionData data,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        DateOnly asOf)
    {
        var quoteDate = data.Marketdata?.SysTime is { } sysTime
            ? DateOnly.FromDateTime(sysTime)
            : asOf;
        var tradingCurrencyRateToRub = GetCurrencyRate(rates, data.Position.CurrencyId, quoteDate);
        var faceCurrencyRateToRub = data.Security is null
            ? null
            : GetCurrencyRate(rates, data.Security.FaceUnit, quoteDate);
        var marketPrice = data.Security is null || data.Marketdata is null
            ? null
            : PortfolioMarketPriceCalculator.Calculate(
                data.Security.FaceValue,
                data.Security.FaceUnit,
                data.Position.CurrencyId,
                data.Marketdata.CurrentPrice,
                faceCurrencyRateToRub,
                tradingCurrencyRateToRub);

        return new PositionMarketPriceData(
            quoteDate,
            tradingCurrencyRateToRub,
            faceCurrencyRateToRub,
            marketPrice);
    }

    internal static bool HasCurrentMarketPrice(PositionData data) =>
        data.Marketdata?.CurrentPrice is { } price && double.IsFinite(price) && price > 0;
}
