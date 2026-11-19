using Popo.Api.Services.Position.Builders;
using Popo.Core;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Api.Services.Position;

public sealed class OpenPositionReferenceDataLoader(
    IPortfolioCouponProvider couponProvider,
    IPortfolioCurrencyRatesProvider currencyRatesProvider,
    IPositionBuilderFactory positionBuilderFactory)
{
    public async Task<OpenPositionReferenceData> LoadAsync(
        IReadOnlyList<IGrouping<SecBoard, PortfolioTradeRecord>> groups,
        IReadOnlyDictionary<SecBoard, MoexBond> securities,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var coupons = await LoadCouponsAsync(groups, securities, asOf, cancellationToken);
        var currentRates = await LoadCurrentRatesAsync(groups, securities, coupons, asOf, cancellationToken);
        var historicalRates = await LoadHistoricalRatesAsync(groups, securities, coupons, asOf, cancellationToken);

        return new OpenPositionReferenceData(coupons, currentRates, currentRates.Concat(historicalRates).ToArray());
    }

    private async Task<ILookup<string, OpenPositionCoupon>> LoadCouponsAsync(
        IReadOnlyList<IGrouping<SecBoard, PortfolioTradeRecord>> groups,
        IReadOnlyDictionary<SecBoard, MoexBond> securities,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var secIds = groups
            .Where(group => positionBuilderFactory.Get(securities[group.Key].BondType) is FloaterOpenPositionBuilder or CurrencyOpenPositionBuilder)
            .Select(group => group.Key.SecId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var coupons = secIds.Length == 0
            ? []
            : await couponProvider.GetCouponScheduleAsync(secIds, asOf, cancellationToken);
        return coupons.ToLookup(coupon => coupon.SecId, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<PortfolioCurrencyRateRecord>> LoadCurrentRatesAsync(
        IReadOnlyList<IGrouping<SecBoard, PortfolioTradeRecord>> groups,
        IReadOnlyDictionary<SecBoard, MoexBond> securities,
        ILookup<string, OpenPositionCoupon> coupons,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var currencies = groups
            .SelectMany(group => positionBuilderFactory.Get(securities[group.Key].BondType) is CurrencyOpenPositionBuilder
                ? new[] { group.First().CurrencyId, securities[group.Key].FaceUnit }
                    .Concat(coupons[group.Key.SecId].Select(coupon => coupon.FaceUnit))
                : [group.First().CurrencyId])
            .Where(currency => !string.IsNullOrWhiteSpace(currency) && !PositionCurrencyRates.IsRubleCurrency(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return currencies.Length == 0
            ? []
            : await currencyRatesProvider.GetLatestRatesAsync(currencies, asOf, cancellationToken);
    }

    private async Task<IReadOnlyList<PortfolioCurrencyRateRecord>> LoadHistoricalRatesAsync(
        IReadOnlyList<IGrouping<SecBoard, PortfolioTradeRecord>> groups,
        IReadOnlyDictionary<SecBoard, MoexBond> securities,
        ILookup<string, OpenPositionCoupon> coupons,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var requests = groups
            .Where(group => positionBuilderFactory.Get(securities[group.Key].BondType) is CurrencyOpenPositionBuilder)
            .SelectMany(group => coupons[group.Key.SecId]
                .Where(coupon => coupon.CouponDate <= asOf && coupon.CouponDate > group.Min(trade => trade.TradeDate)
                    && !PositionCurrencyRates.IsRubleCurrency(group.First().CurrencyId)
                    && !string.Equals(coupon.FaceUnit, group.First().CurrencyId, StringComparison.OrdinalIgnoreCase))
                .Select(coupon => (coupon.CouponDate, Currency: group.First().CurrencyId)))
            .GroupBy(request => request.CouponDate);

        var rates = new List<PortfolioCurrencyRateRecord>();
        foreach (var request in requests)
        {
            var currencies = request.Select(item => item.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            rates.AddRange(await currencyRatesProvider.GetLatestRatesAsync(currencies, request.Key, cancellationToken));
        }
        return rates;
    }
}

public sealed record OpenPositionReferenceData(
    ILookup<string, OpenPositionCoupon> CouponsBySecId,
    IReadOnlyList<PortfolioCurrencyRateRecord> CurrentRates,
    IReadOnlyList<PortfolioCurrencyRateRecord> AllRates);
