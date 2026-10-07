using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Models;

public enum BrokerReportOperationKind
{
    BondTrade,
    FundOperation,
    Deposit,
    Withdrawal
}

public sealed record BrokerReportOperationResponse(
    Guid Id,
    BrokerReportOperationKind Kind,
    DateOnly Date,
    string Description,
    string? SecId,
    string? BoardId,
    string? CurrencyId,
    TradeSide? Side,
    double? Quantity,
    double? UnitPrice,
    double? FaceValue,
    double? AccruedInterestTotal,
    double? Commission,
    double? Amount,
    bool CanImport,
    string? Warning);

public sealed record BrokerReportPreviewResponse(
    IReadOnlyList<BrokerReportOperationResponse> Operations,
    int DuplicatesHidden,
    int OutsideHistoryHidden,
    int UnsupportedOperationsHidden);

public sealed record BrokerReportImportRequest(
    IReadOnlyList<BrokerReportOperationRequest> Operations);

public sealed record BrokerReportOperationRequest(
    Guid Id,
    BrokerReportOperationKind Kind,
    DateOnly Date,
    string? SecId,
    string? BoardId,
    string? CurrencyId,
    TradeSide? Side,
    double? Quantity,
    double? UnitPrice,
    double? FaceValue,
    double? AccruedInterestTotal,
    double? Commission,
    double? Amount);

public sealed record BrokerReportImportResponse(
    int TradesAdded,
    int FundOperationsAdded,
    int CashFlowsAdded);

public sealed record BrokerReportImportData(
    PortfolioTrade? Trade,
    MoneyMarketFundOperation? FundOperation,
    PortfolioCashFlowInput? CashFlow,
    BrokerReportOperationRequest Source);
