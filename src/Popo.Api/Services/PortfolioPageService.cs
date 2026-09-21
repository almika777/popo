using Popo.Api.Models;
using Popo.Core.PortfolioReturns;

namespace Popo.Api.Services;

public sealed class PortfolioPageService(
    IPortfolioReturnInputsProvider inputsProvider,
    IPortfolioPositionsService positionsService)
{
    public async Task<PortfolioPageResponse> GetAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var valuationsTask = inputsProvider.GetValuationsAsync(cancellationToken);
        var cashFlowsTask = inputsProvider.GetCashFlowsAsync(cancellationToken);
        var overviewTask = positionsService.GetOverviewAsync(asOf, cancellationToken);
        await Task.WhenAll(valuationsTask, cashFlowsTask, overviewTask);

        var overview = await overviewTask;
        return new PortfolioPageResponse(
            await valuationsTask,
            await cashFlowsTask,
            overview.Balances,
            overview.Summary);
    }
}
