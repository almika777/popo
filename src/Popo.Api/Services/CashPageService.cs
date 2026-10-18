using Popo.Api.Models;
using Popo.Core.Bonds;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class CashPageService(
    IPortfolioCashProvider cashProvider,
    IPortfolioService portfolioService,
    IBondsService bondsService)
{
    public async Task<CashPageResponse> GetAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var snapshotsTask = cashProvider.GetCashSnapshotsAsync(cancellationToken);
        var balancesTask = portfolioService.GetCashBalancesAsync(asOf, cancellationToken);
        var currenciesTask = bondsService.GetTradingCurrenciesAsync(cancellationToken);
        await Task.WhenAll(snapshotsTask, balancesTask, currenciesTask);

        return new CashPageResponse(
            await snapshotsTask,
            await balancesTask,
            await currenciesTask);
    }
}
