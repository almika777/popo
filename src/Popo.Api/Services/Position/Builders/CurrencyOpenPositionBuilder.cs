using Popo.Api.Models;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Api.Services.Position.Builders;

public sealed class CurrencyOpenPositionBuilder : IOpenPositionBuilder
{
    public PositionResponse Build(OpenPositionBuildInput input)
    {
        var balance = CalculateOpenBalance(input);
        var first = input.Trades[0];
        var marketPrice = input.Security.FaceValue * input.Marketdata.CurrentPrice!.Value / 100
            * CurrencyRate(input, input.Security.FaceUnit, first.CurrencyId, input.AsOf);
        var totalCost = balance.Quantity * marketPrice;
        var unrealizedPnl = totalCost - balance.CleanCost;

        return new PositionResponse
        {
            SecId = first.SecId,
            BoardId = first.BoardId,
            CurrencyId = first.CurrencyId,
            ShortName = string.IsNullOrWhiteSpace(input.Security.ShortName) ? first.SecId : input.Security.ShortName,
            Quantity = balance.Quantity,
            AvgBuyPrice = balance.CleanCost / balance.Quantity,
            MarketPrice = marketPrice,
            TotalCost = totalCost,
            UnrealizedPnl = unrealizedPnl,
            CouponIncome = balance.CouponIncome,
            TotalPnl = unrealizedPnl + balance.CouponIncome - balance.BuyCommission
        };
    }

    private static OpenBalance CalculateOpenBalance(OpenPositionBuildInput input)
    {
        var trades = input.Trades.OrderBy(trade => trade.TradeDate).ThenBy(trade => trade.Id).ToArray();
        var lastClosedTrade = FindLastClosedTrade(trades);
        var quantity = 0;
        double cleanCost = 0;
        double buyCommission = 0;
        double couponIncome = 0;

        foreach (var trade in trades.Skip(lastClosedTrade + 1))
        {
            if (trade.Side == TradeSide.Buy)
            {
                quantity += trade.Quantity;
                cleanCost += trade.Quantity * trade.Price;
                buyCommission += trade.Commission;
                couponIncome += trade.Quantity * CalculateCouponPerBond(trade.TradeDate, input);
                continue;
            }

            var fraction = (double)(quantity - trade.Quantity) / quantity;
            quantity -= trade.Quantity;
            cleanCost *= fraction;
            buyCommission *= fraction;
            couponIncome *= fraction;
        }

        return new OpenBalance(quantity, cleanCost, buyCommission, couponIncome);
    }

    private static int FindLastClosedTrade(IReadOnlyList<PortfolioTradeRecord> trades)
    {
        var quantity = 0;
        var lastClosedTrade = -1;
        for (var i = 0; i < trades.Count; i++)
        {
            quantity += trades[i].Side == TradeSide.Buy ? trades[i].Quantity : -trades[i].Quantity;
            if (quantity == 0)
                lastClosedTrade = i;
        }

        return lastClosedTrade;
    }

    private static double CalculateCouponPerBond(DateOnly bought, OpenPositionBuildInput input)
    {
        double income = 0;
        foreach (var period in input.Coupons.Where(period => period.StartDate <= input.AsOf && period.CouponDate > bought))
        {
            var from = Math.Max(bought.DayNumber, period.StartDate.DayNumber);
            var to = Math.Min(input.AsOf.DayNumber + 1, period.CouponDate.DayNumber);
            var amount = GetCouponAmount(period, input);
            income += (to - from) * amount / (period.CouponDate.DayNumber - period.StartDate.DayNumber);
        }

        return income;
    }

    private static double GetCouponAmount(OpenPositionCoupon period, OpenPositionBuildInput input)
    {
        var currency = input.Trades[0].CurrencyId;
        if (SameCurrency(period.FaceUnit, currency))
            return period.Value!.Value;

        return period.CouponDate <= input.AsOf
            ? period.ValueInRub!.Value * CurrencyRate(input, "RUB", currency, period.CouponDate)
            : period.Value!.Value * CurrencyRate(input, period.FaceUnit, currency, input.AsOf);
    }

    private static double CurrencyRate(OpenPositionBuildInput input, string source, string target, DateOnly date)
        => SameCurrency(source, target) ? 1 : RateToRub(input, source, date) / RateToRub(input, target, date);

    private static double RateToRub(OpenPositionBuildInput input, string currency, DateOnly date)
    {
        if (PositionCurrencyRates.IsRubleCurrency(currency))
            return 1;

        return input.CurrencyRates
            .Concat(input.CurrentCurrencyRate is null ? [] : [input.CurrentCurrencyRate])
            .Where(rate => string.Equals(rate.CurrencyId, currency, StringComparison.OrdinalIgnoreCase) && rate.RateDate <= date)
            .OrderByDescending(rate => rate.RateDate)
            .First().RateToRub;
    }

    private static bool SameCurrency(string first, string second)
        => string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
           || PositionCurrencyRates.IsRubleCurrency(first) && PositionCurrencyRates.IsRubleCurrency(second);

    private sealed record OpenBalance(int Quantity, double CleanCost, double BuyCommission, double CouponIncome);
}
