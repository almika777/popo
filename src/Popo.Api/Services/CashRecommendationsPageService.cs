using Popo.Api.Models;
using Popo.Core.Bonds;
using Popo.Core.Recommendations;

namespace Popo.Api.Services;

public sealed class CashRecommendationsPageService(
    ICashInvestmentRecommendationStore store,
    IBondsService bondsService)
{
    public async Task<CashRecommendationsPageResponse> GetAsync(CancellationToken cancellationToken)
    {
        var presetsTask = store.GetPresetsAsync(cancellationToken);
        var tradingCurrenciesTask = bondsService.GetTradingCurrenciesAsync(cancellationToken);
        var faceUnitsTask = bondsService.GetFaceUnitsAsync(cancellationToken);
        await Task.WhenAll(presetsTask, tradingCurrenciesTask, faceUnitsTask);

        return new CashRecommendationsPageResponse(
            (await presetsTask).Select(CashInvestmentRecommendationApiMapper.ToResponse).ToArray(),
            await tradingCurrenciesTask,
            await faceUnitsTask);
    }
}
