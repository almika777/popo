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
    double FaceValue = 100,
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

public sealed record PortfolioPosition(
    string SecId,
    string BoardId,
    string CurrencyId,
    double Quantity,
    double BoughtQuantity,
    double SoldQuantity,
    double BoughtCleanAmount,
    double BoughtAmount,
    double SoldAmount);

public sealed record CashSnapshot(
    string CurrencyId,
    DateOnly SnapshotDate,
    double Amount);

public sealed record CashBalance(
    string CurrencyId,
    double Amount);

public sealed record MoneyMarketFund(
    string SecId,
    string BoardId,
    double Quantity,
    double AveragePrice);

public sealed record MoneyMarketFundOperation(
    string SecId,
    DateOnly Date,
    TradeSide Side,
    double Quantity,
    double Price,
    double Commission);

public sealed record MoneyMarketFundOperationRecord(
    Guid Id,
    string SecId,
    DateOnly Date,
    TradeSide Side,
    double Quantity,
    double Price,
    double Commission,
    double Amount);

public sealed record MoneyMarketFundRecord(
    Guid Id,
    string SecId,
    string BoardId,
    double Quantity,
    double AveragePrice,
    double? CurrentPrice = null,
    double? CurrentValue = null,
    double? Pnl = null,
    double? PnlPercent = null,
    DateTime? QuoteTime = null);

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

public sealed record CashSnapshotRecord(
    Guid Id,
    string CurrencyId,
    DateOnly SnapshotDate,
    double Amount,
    string Comment);

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
    double? MarketPricePercent = null);

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

public interface IPortfolioLedgerProvider
{
    Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken);
    Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken);
    Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken);
    Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken);
    Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CashSnapshotRecord>> GetCashSnapshotsAsync(CancellationToken cancellationToken);
    Task<CashSnapshotRecord?> GetCashSnapshotAsync(Guid id, CancellationToken cancellationToken);
    Task<CashSnapshotRecord> AddCashSnapshotAsync(CashSnapshot snapshot, string comment, CancellationToken cancellationToken);
    Task<CashSnapshotRecord?> UpdateCashSnapshotAsync(Guid id, CashSnapshot snapshot, string comment, CancellationToken cancellationToken);
    Task<bool> DeleteCashSnapshotAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CashBalance>> GetCashBalancesAsync(DateOnly asOf, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken);
    Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken);
    Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken);
    Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(CancellationToken cancellationToken);
    Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(MoneyMarketFundOperation operation, CancellationToken cancellationToken);
}
