using Popo.Core.PortfolioReturns;

namespace Popo.Core.Portfolio;

public sealed record BrokerReportImportBatch(
    IReadOnlyList<BrokerReportImportItem<PortfolioTrade>> Trades,
    IReadOnlyList<BrokerReportImportItem<MoneyMarketFundOperation>> FundOperations,
    IReadOnlyList<BrokerReportImportItem<PortfolioCashFlowInput>> CashFlows);

public sealed record BrokerReportImportItem<T>(Guid Id, T Operation);

public sealed record BrokerReportImportResult(
    int TradesAdded,
    int FundOperationsAdded,
    int CashFlowsAdded);

public interface IBrokerReportImportProvider
{
    Task<BrokerReportImportResult> AddAsync(BrokerReportImportBatch batch, CancellationToken cancellationToken);
}
