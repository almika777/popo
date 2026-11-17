using Popo.Api.Models;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Api.Services.Position.Builders;

public class DefaultOpenPositionBuilder : IOpenPositionBuilder
{
    public PositionResponse Build(OpenPositionBuildInput input)
    {
        var balance = CalculateOpenBalance(input.Trades, input.AsOf);
        var quantity = input.Trades.Sum(trade => trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity);
        return CreateResponse(input.Trades[0], quantity, balance, input.Marketdata, input.Security);
    }

    private static OpenBalance CalculateOpenBalance(IReadOnlyList<PortfolioTradeRecord> trades, DateOnly asOf)
    {
        double remainingQuantity = 0;
        double remainingCleanCost = 0;
        double remainingBuyCommission = 0;
        double remainingHoldingDays = 0;
        foreach (var trade in trades.OrderBy(trade => trade.TradeDate).ThenBy(trade => trade.Id))
        {
            if (trade.Side == TradeSide.Buy)
            {
                remainingQuantity += trade.Quantity;
                remainingCleanCost += trade.Quantity * trade.Price;
                remainingBuyCommission += trade.Commission;
                remainingHoldingDays += (double)trade.Quantity * (asOf.DayNumber - trade.TradeDate.DayNumber + 1);
                continue;
            }

            var fraction = (remainingQuantity - trade.Quantity) / remainingQuantity;
            remainingQuantity -= trade.Quantity;
            remainingCleanCost *= fraction;
            remainingBuyCommission *= fraction;
            remainingHoldingDays *= fraction;
        }

        return new OpenBalance(remainingCleanCost, remainingBuyCommission, remainingHoldingDays);
    }

    private static PositionResponse CreateResponse(
        PortfolioTradeRecord first,
        int quantity,
        OpenBalance balance,
        MoexMarketdata marketdata,
        MoexBond security)
    {
        var quote = marketdata.CurrentPrice!.Value;
        var unrealizedPnl = quantity * security.FaceValue * quote / 100 - balance.CleanCost;
        var couponIncome = balance.HoldingDays * security.CouponValue / security.CouponPeriod;

        return new PositionResponse
        {
            SecId = first.SecId,
            BoardId = first.BoardId,
            CurrencyId = first.CurrencyId,
            ShortName = string.IsNullOrWhiteSpace(security.ShortName) ? first.SecId : security.ShortName,
            Quantity = quantity,
            AvgBuyPrice = balance.CleanCost / quantity,
            MarketPrice = security.FaceValue * quote / 100,
            TotalCost = quantity * security.FaceValue * quote / 100,
            UnrealizedPnl = unrealizedPnl,
            CouponIncome = couponIncome,
            TotalPnl = unrealizedPnl + couponIncome - balance.Commission
        };
    }

    private sealed record OpenBalance(double CleanCost, double Commission, double HoldingDays);
}
