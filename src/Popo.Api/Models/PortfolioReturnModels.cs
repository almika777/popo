using Popo.Core.PortfolioReturns;
using Popo.Core.Portfolio;

namespace Popo.Api.Models;

public sealed record UpsertPortfolioValuationRequest(
    DateOnly Date,
    double TotalValue,
    string? Comment);

public sealed record UpsertPortfolioCashFlowRequest(
    DateOnly Date,
    PortfolioCashFlowType Type,
    double Amount,
    string? Comment);

public sealed record PortfolioPageResponse(
    IReadOnlyList<PortfolioValuationRecord> Valuations,
    IReadOnlyList<PortfolioCashFlowRecord> CashFlows,
    IReadOnlyList<CashBalance> Balances,
    PortfolioSummaryRecord Summary);
