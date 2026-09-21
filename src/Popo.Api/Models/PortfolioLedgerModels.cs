using Popo.Core.Portfolio;

namespace Popo.Api.Models;

public sealed record UpsertPortfolioTradeRequest(
    string SecId,
    string BoardId,
    string CurrencyId,
    DateOnly TradeDate,
    TradeSide Side,
    double Quantity,
    double Price,
    double FaceValue,
    double AccruedInterestTotal,
    double CommissionPercent);

public sealed record UpsertCashSnapshotRequest(
    string CurrencyId,
    DateOnly SnapshotDate,
    double Amount,
    string? Comment);

public sealed record UpsertMoneyMarketFundRequest(
    string SecId,
    double Quantity,
    double AveragePrice);

public sealed record AddMoneyMarketFundOperationRequest(
    string SecId,
    DateOnly Date,
    TradeSide Side,
    double Quantity,
    double Price,
    double Commission);

public sealed record PositionsPageResponse(
    IReadOnlyList<PortfolioPositionRecord> Positions,
    PositionTotalsResponse Totals,
    PositionRecommendationStateResponse Recommendation);

public sealed record PositionTotalsResponse(
    double MarketValueRub,
    double InvestedAmountRub,
    double UnrealizedPnlRub,
    double UnrealizedPnlPercent);

public sealed record MoneyMarketFundOptionResponse(string SecId, string BoardId, string Name);

public sealed record CashPageResponse(
    IReadOnlyList<CashSnapshotRecord> Snapshots,
    IReadOnlyList<CashBalance> Balances,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<MoneyMarketFundRecord> MoneyMarketFunds,
    IReadOnlyList<MoneyMarketFundOptionResponse> MoneyMarketFundOptions,
    IReadOnlyList<MoneyMarketFundOperationRecord> MoneyMarketFundOperations);
