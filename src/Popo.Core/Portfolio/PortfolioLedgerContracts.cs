namespace Popo.Core.Portfolio;

public enum TradeSide
{
    Buy,
    Sell
}

public sealed record PortfolioTrade(
    string SecId,
    string BoardId,
    string CurrencyId,
    DateOnly TradeDate,
    TradeSide Side,
    double Quantity,
    double Price,
    double FaceValue = 1_000,
    double AccruedInterest = 0,
    double CommissionPercent = 0)
{
    public double CleanAmount => Quantity * Price;
    public double AccruedInterestAmount => Quantity * AccruedInterest;
    public double GrossAmount => TradeSettlementCalculator.Calculate(Side, Quantity, Price, AccruedInterest, CommissionPercent).GrossAmount;
    public double CommissionAmount => TradeSettlementCalculator.Calculate(Side, Quantity, Price, AccruedInterest, CommissionPercent).CommissionAmount;
    public double Amount => TradeSettlementCalculator.Calculate(Side, Quantity, Price, AccruedInterest, CommissionPercent).NetAmount;
    public double CashDelta => Side == TradeSide.Buy ? -Amount : Amount;
}

public sealed record PortfolioTradeRecord(
    Guid Id,
    string SecId,
    string BoardId,
    string CurrencyId,
    DateOnly TradeDate,
    TradeSide Side,
    double Quantity,
    double Price,
    double FaceValue,
    double AccruedInterest,
    double Commission,
    double CommissionPercent,
    double Amount,
    string ShortName = "");

public sealed record PortfolioPositionRecord(
    string SecId,
    string BoardId,
    string CurrencyId,
    double Quantity,
    double BoughtQuantity,
    double SoldQuantity,
    double BoughtCleanAmount,
    double BoughtAmount,
    double SoldAmount,
    double AverageBuyPrice,
    double? MarketPrice = null,
    double? MarketValue = null,
    double? UnrealizedPnl = null,
    double? UnrealizedPnlPercent = null,
    string ShortName = "",
    double? AverageDailyVolume = null,
    double? MedianDailyVolume = null,
    double? ApproximateCouponIncome = null,
    double? ApproximateTotalPnl = null,
    double? ApproximateTotalPnlPercent = null,
    double? AverageBuyPriceAtCurrentFaceValue = null,
    double? AverageBuyPricePercent = null,
    double? CurrentFaceValue = null,
    string? FaceUnit = null,
    double? MarketPricePercent = null,
    bool IsNominalIndexed = false,
    double? ApproximateAnnualizedTotalPnlPercent = null);

public sealed record PortfolioPositionValuation(
    double? MarketPrice,
    double? MarketValue,
    double? UnrealizedPnl,
    double? UnrealizedPnlPercent);

public sealed record PortfolioSummaryRecord(
    DateOnly AsOf,
    double TotalValueRub,
    double PositionsValueRub,
    double CashValueRub,
    double MoneyMarketFundsValueRub,
    double UnrealizedPnlRub);
