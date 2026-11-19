using Popo.Api.Models;

namespace Popo.Api.Services.Position.Builders;

using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

public class FloaterOpenPositionBuilder : IOpenPositionBuilder
{
    public PositionResponse Build(OpenPositionBuildInput input)
    {
        var balance = CalculateOpenBalance(input);
        var first = input.Trades[0];
        var quantity = input.Trades.Sum(trade => trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity);
        var marketPrice = input.Security.FaceValue * input.Marketdata.CurrentPrice!.Value / 100;
        var totalCost = quantity * marketPrice;
        var unrealizedPnl = totalCost - balance.CleanCost;

        return new PositionResponse
        {
            SecId = first.SecId,
            BoardId = first.BoardId,
            CurrencyId = first.CurrencyId,
            ShortName = string.IsNullOrWhiteSpace(input.Security.ShortName) ? first.SecId : input.Security.ShortName,
            Quantity = quantity,
            AvgBuyPrice = balance.CleanCost / quantity,
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
        var quantity = 0;
        var lastClosedTrade = -1;
        for (var i = 0; i < trades.Length; i++)
        {
            quantity += trades[i].Side == TradeSide.Buy ? trades[i].Quantity : -trades[i].Quantity;
            if (quantity == 0)
                lastClosedTrade = i;
        }

        double remaining = 0;
        double cleanCost = 0;
        double commission = 0;
        double couponIncome = 0;
        foreach (var trade in trades.Skip(lastClosedTrade + 1))
        {
            if (trade.Side == TradeSide.Buy)
            {
                remaining += trade.Quantity;
                cleanCost += trade.Quantity * trade.Price;
                commission += trade.Commission;
                couponIncome += trade.Quantity * CalculateCouponPerBond(trade.TradeDate, input.AsOf, input.Coupons);
                continue;
            }

            var fraction = (remaining - trade.Quantity) / remaining;
            remaining -= trade.Quantity;
            cleanCost *= fraction;
            commission *= fraction;
            couponIncome *= fraction;
        }

        return new OpenBalance(cleanCost, commission, couponIncome);
    }

    private static double CalculateCouponPerBond(DateOnly bought, DateOnly asOf,
        IReadOnlyList<OpenPositionCoupon> coupons)
    {
        double result = 0;
        foreach (var period in coupons.Where(period => period.StartDate <= asOf && period.CouponDate > bought))
        {
            var from = Math.Max(bought.DayNumber, period.StartDate.DayNumber);
            var to = Math.Min(asOf.DayNumber + 1, period.CouponDate.DayNumber);
            var amount = period.Value ?? coupons
                .Where(previous => previous.CouponDate <= period.StartDate && previous.Value.HasValue)
                .MaxBy(previous => previous.CouponDate)!.Value!.Value;
            result += (to - from) * amount / (period.CouponDate.DayNumber - period.StartDate.DayNumber);
        }

        return result;
    }

    private sealed record OpenBalance(double CleanCost, double BuyCommission, double CouponIncome);
}
