using Popo.Api.Models;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class CashPageService(
    IPortfolioLedgerProvider ledgerProvider,
    IPortfolioPositionsService positionsService,
    IBondsService bondsService)
{
    public async Task<CashPageResponse> GetAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var snapshotsTask = ledgerProvider.GetCashSnapshotsAsync(cancellationToken);
        var balancesTask = ledgerProvider.GetCashBalancesAsync(asOf, cancellationToken);
        var currenciesTask = bondsService.GetTradingCurrenciesAsync(cancellationToken);
        var fundsTask = positionsService.GetMoneyMarketFundsAsync(cancellationToken);
        var operationsTask = ledgerProvider.GetMoneyMarketFundOperationsAsync(cancellationToken);
        await Task.WhenAll(snapshotsTask, balancesTask, currenciesTask, fundsTask, operationsTask);

        var options = RelationHelper.MoneyMarketFunds
            .Select(x => new MoneyMarketFundOptionResponse(x.Key, x.Value.BoardId, x.Value.Name))
            .ToArray();
        return new CashPageResponse(
            await snapshotsTask,
            await balancesTask,
            await currenciesTask,
            await fundsTask,
            options,
            await operationsTask);
    }
}
