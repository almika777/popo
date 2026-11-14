using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Services.Position;

public class OpenPositionService(
    IPortfolioTradesProvider portfolioTradesProvider,
    IMoexHttpClient moexHttpClient,
    IPositionBuilderFactory positionBuilderFactory,
    IPortfolioCouponProvider couponProvider,
    IPortfolioCurrencyRatesProvider currencyRatesProvider) : IOpenPositionsService
{
    public async Task<PositionsPageResponse> GetAsync(CancellationToken cancellationToken)
    {
        var trades = await portfolioTradesProvider.GetTradesAsync(cancellationToken);
        var securities = (await moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .ToDictionary(x => new SecBoard(x.SecId, x.BoardId));

        var marketdatas = (await moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken))
            .SelectMany(x => x.Value)
            .ToDictionary(x => new SecBoard(x.SecId, x.BoardId));

        var asOf = MoscowTime.Today;
        var floaterSecIds = trades.GroupBy(trade => new SecBoard(trade.SecId, trade.BoardId))
            .Where(group => group.Sum(trade => trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity) != 0)
            .Where(group => securities.GetValueOrDefault(group.Key)?.BondType is "Флоатер" or "Облигация с плавающим купоном")
            .Select(group => group.Key.SecId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var coupons = floaterSecIds.Length == 0
            ? []
            : await couponProvider.GetCouponScheduleAsync(floaterSecIds, asOf, cancellationToken);
        var couponsBySecId = coupons.ToLookup(coupon => coupon.SecId, StringComparer.Ordinal);

        var res = new List<PositionResponse>();
        foreach (var portfolioTradeRecords in trades.GroupBy(x => new SecBoard(x.SecId, x.BoardId)))
        {
            var quantity = portfolioTradeRecords
                .Sum(trade => trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity);

            var security = securities.GetValueOrDefault(portfolioTradeRecords.Key);
            if (security == null || quantity == 0)
                continue;
            
            var marketdata = marketdatas[portfolioTradeRecords.Key];

            var builder = positionBuilderFactory.Get(security.BondType);
            var position = builder.Build(new OpenPositionBuildInput
            {
                Trades = portfolioTradeRecords.ToList(),
                Marketdata = marketdata,
                Security = security,
                AsOf = asOf,
                Coupons = couponsBySecId[portfolioTradeRecords.Key.SecId].ToArray()
            });

            res.Add(position with { BondType = security.BondType });
        }

        var currencies = res.Select(position => position.CurrencyId)
            .Where(currency => !PositionCurrencyRates.IsRubleCurrency(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = currencies.Length == 0
            ? []
            : await currencyRatesProvider.GetLatestRatesAsync(currencies, asOf, cancellationToken);
        return new PositionsPageResponse(res, OpenPositionTotalsCalculator.Calculate(res, rates));
    }
}
