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
    double SellAmount)
{
    public double? AverageBuyPrice { get; init; }
}

public sealed record PositionIncomeTrade(
    DateOnly TradeDate,
    TradeSide Side,
    double Quantity,
    double CleanPrice,
    double Commission,
    double? CleanPricePercent = null,
    double? ActualCleanPrice = null,
    double AccruedInterest = 0);

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
    double? AnnualizedTotalPnlPercent = null,
    double HistoricalCouponIncome = 0,
    double PaidBuyAccruedInterest = 0,
    double CurrentAccruedInterest = 0,
    bool IsCouponIncomeComplete = true,
    PositionAnnualizedReturnBreakdown? AnnualizedReturnBreakdown = null);
