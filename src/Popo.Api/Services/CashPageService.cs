using Popo.Api.Models;
using Popo.Api.Services.Position;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class CashPageService(
    IPortfolioCashProvider cashProvider,
    IPortfolioService portfolioService,
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    IPortfolioPositionsService positionsService,
    IBondsService bondsService)
{
    public async Task<CashPageResponse> GetAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var snapshotsTask = cashProvider.GetCashSnapshotsAsync(cancellationToken);
        var balancesTask = portfolioService.GetCashBalancesAsync(asOf, cancellationToken);
        var currenciesTask = bondsService.GetTradingCurrenciesAsync(cancellationToken);
        var fundsTask = positionsService.GetMoneyMarketFundsAsync(cancellationToken);
        var operationsTask = moneyMarketFundsProvider.GetMoneyMarketFundOperationsAsync(cancellationToken);
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
