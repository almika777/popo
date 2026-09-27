using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Services;

public interface IPortfolioService
{
    Task<IReadOnlyList<CashBalance>> GetCashBalancesAsync(DateOnly asOf, CancellationToken cancellationToken);
}

public sealed class PortfolioService(
    IPortfolioCashProvider cashProvider,
    IPortfolioTradesProvider tradesProvider,
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    IPortfolioCashFlowsProvider cashFlowsProvider) : IPortfolioService
{
    public async Task<IReadOnlyList<CashBalance>> GetCashBalancesAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var snapshots = await cashProvider.GetCashSnapshotsAsync(cancellationToken);
        var trades = await tradesProvider.GetTradesAsync(cancellationToken);
        var fundOperations = await moneyMarketFundsProvider.GetMoneyMarketFundOperationsAsync(cancellationToken);
        var cashFlows = await cashFlowsProvider.GetCashFlowsAsync(cancellationToken);

        return PortfolioCashCalculator.Calculate(
            snapshots.Select(x => new CashSnapshot(x.CurrencyId, x.SnapshotDate, x.Amount)).ToArray(),
            trades.Select(x => new PortfolioTrade(
                x.SecId,
                x.BoardId,
                x.CurrencyId,
                x.TradeDate,
                x.Side,
                x.Quantity,
                x.Price,
                x.FaceValue,
                x.AccruedInterest,
                x.CommissionPercent)).ToArray(),
            asOf,
            fundOperations.Select(x => new MoneyMarketFundOperation(
                x.SecId, x.Date, x.Side, x.Quantity, x.Price, x.Commission)).ToArray(),
            cashFlows.Select(x => new PortfolioCashFlowInput(x.Date, x.Type, x.Amount)).ToArray());
    }
}
