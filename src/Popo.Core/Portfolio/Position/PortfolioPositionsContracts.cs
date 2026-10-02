namespace Popo.Core.Portfolio.Position;

public sealed record PortfolioPosition(
    string SecId,
    string BoardId,
    string CurrencyId,
    double Quantity,
    double BuyQuantity,
    double SellQuantity,
    double BuyCleanAmount,
    double BuyAmount,
    double SellAmount);

public sealed record PositionIncomeTrade(
    DateOnly TradeDate,
    TradeSide Side,
    double Quantity,
    double CleanPrice,
    double Commission,
    double? CleanPricePercent = null,
    double? ActualCleanPrice = null);

public sealed record PortfolioPositionIncome(
    double RemainingCleanCost,
    double PaidBuyCommission,
    double CouponIncome,
    double TotalPnl,
    double TotalPnlPercent,
    double RemainingQuantity = 0,
    double? AverageBuyPricePercent = null,
    double? UnrealizedPnl = null,
    double? UnrealizedPnlPercent = null,
    double? ActualRemainingCleanCost = null,
    double? AnnualizedTotalPnlPercent = null);
