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

