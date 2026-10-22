using Popo.Api.Models;
using Popo.Api.Services.Position;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class MoneyMarketFundsPageService(
    IPortfolioPositionsService positionsService)
{
    /// <summary>Загружает позиции с котировками MOEX, добавляет варианты фондов и собирает ответ страницы.</summary>
    /// <param name="cancellationToken">Токен отмены загрузки данных.</param>
    /// <returns>Единый ответ для страницы фондов денежного рынка.</returns>
    public async Task<MoneyMarketFundsPageResponse> GetAsync(CancellationToken cancellationToken)
    {
        var funds = await positionsService.GetMoneyMarketFundsAsync(cancellationToken);

        var options = RelationHelper.MoneyMarketFunds
            .Select(x => new MoneyMarketFundOptionResponse(x.Key, x.Value.BoardId, x.Value.Name))
            .ToArray();

        return new MoneyMarketFundsPageResponse(funds, options);
    }
}
