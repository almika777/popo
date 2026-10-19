using Popo.Api.Models;
using Popo.Api.Services.Position;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class MoneyMarketFundsPageService(
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    IPortfolioPositionsService positionsService)
{
    /// <summary>Параллельно загружает позиции с котировками MOEX и историю операций,
    /// добавляет варианты фондов и собирает ответ страницы.</summary>
    /// <param name="cancellationToken">Токен отмены загрузки данных.</param>
    /// <returns>Единый ответ для страницы фондов денежного рынка.</returns>
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
