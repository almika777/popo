using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class MoneyMarketFundManagementService(
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider)
{
    public async Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(
        UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var secId = request.SecId.Trim();
        if (!RelationHelper.MoneyMarketFunds.TryGetValue(secId, out var definition))
            throw new InvalidApplicationRequestException("Поддерживаются только LQDT и TMON.");

        var existing = await moneyMarketFundsProvider.GetMoneyMarketFundsAsync(cancellationToken);
        if (existing.Any(x => string.Equals(x.SecId, secId, StringComparison.OrdinalIgnoreCase)))
            throw new ApplicationConflictException("Этот фонд денежного рынка уже добавлен.");

        return await moneyMarketFundsProvider.AddMoneyMarketFundAsync(
            new MoneyMarketFund(secId, definition.BoardId, request.Quantity, request.AveragePrice), cancellationToken);
    }

    public async Task<MoneyMarketFundRecord> UpdateMoneyMarketFundAsync(
        Guid id,
        UpsertMoneyMarketFundRequest request,
        CancellationToken cancellationToken)
    {
        var secId = request.SecId.Trim();
        if (!RelationHelper.MoneyMarketFunds.TryGetValue(secId, out var definition))
            throw new InvalidApplicationRequestException("Поддерживаются только LQDT и TMON.");

        var existing = await moneyMarketFundsProvider.GetMoneyMarketFundsAsync(cancellationToken);
        if (existing.Any(x => x.Id != id && string.Equals(x.SecId, secId, StringComparison.OrdinalIgnoreCase)))
            throw new ApplicationConflictException("Этот фонд денежного рынка уже добавлен.");

        return await moneyMarketFundsProvider.UpdateMoneyMarketFundAsync(
                   id,
                   new MoneyMarketFund(secId, definition.BoardId, request.Quantity, request.AveragePrice),
                   cancellationToken)
               ?? throw new ApplicationResourceNotFoundException();
    }

    public async Task DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await moneyMarketFundsProvider.DeleteMoneyMarketFundAsync(id, cancellationToken))
            throw new ApplicationResourceNotFoundException();
    }
}
