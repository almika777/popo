using Popo.Api.Models;
using Popo.Api.Services.Position;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class MoneyMarketFundsPageService(
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    IPortfolioPositionsService positionsService)
{
    public async Task<MoneyMarketFundsPageResponse> GetAsync(CancellationToken cancellationToken)
    {
        var fundsTask = positionsService.GetMoneyMarketFundsAsync(cancellationToken);
        var operationsTask = moneyMarketFundsProvider.GetMoneyMarketFundOperationsAsync(cancellationToken);
        await Task.WhenAll(fundsTask, operationsTask);

        var options = RelationHelper.MoneyMarketFunds
            .Select(x => new MoneyMarketFundOptionResponse(x.Key, x.Value.BoardId, x.Value.Name))
            .ToArray();

        return new MoneyMarketFundsPageResponse(await fundsTask, options, await operationsTask);
    }
}
