using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Services;

public sealed class CashManagementService(
    IPortfolioCashProvider cashProvider,
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider)
{
    public async Task<CashSnapshotRecord> AddCashSnapshotAsync(
        UpsertCashSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var currencyId = request.CurrencyId.Trim();
        var snapshots = await cashProvider.GetCashSnapshotsAsync(cancellationToken);
        if (snapshots.Any(x => x.CurrencyId == currencyId && x.SnapshotDate == request.SnapshotDate))
            throw new ApplicationConflictException("Снимок кеша для этой валюты и даты уже существует.");

        return await cashProvider.AddCashSnapshotAsync(
            new CashSnapshot(currencyId, request.SnapshotDate, request.Amount),
            request.Comment?.Trim() ?? string.Empty,
            cancellationToken);
    }

    public async Task<CashSnapshotRecord> UpdateCashSnapshotAsync(
        Guid id,
        UpsertCashSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var currencyId = request.CurrencyId.Trim();
        var snapshots = await cashProvider.GetCashSnapshotsAsync(cancellationToken);
        if (snapshots.All(x => x.Id != id))
            throw new ApplicationResourceNotFoundException();
        if (snapshots.Any(x => x.Id != id && x.CurrencyId == currencyId && x.SnapshotDate == request.SnapshotDate))
            throw new ApplicationConflictException("Снимок кеша для этой валюты и даты уже существует.");

        return await cashProvider.UpdateCashSnapshotAsync(
                   id,
                   new CashSnapshot(currencyId, request.SnapshotDate, request.Amount),
                   request.Comment?.Trim() ?? string.Empty,
                   cancellationToken)
               ?? throw new ApplicationResourceNotFoundException();
    }

    public async Task DeleteCashSnapshotAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await cashProvider.DeleteCashSnapshotAsync(id, cancellationToken))
            throw new ApplicationResourceNotFoundException();
    }

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

    public async Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(
        AddMoneyMarketFundOperationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await moneyMarketFundsProvider.AddMoneyMarketFundOperationAsync(
                new MoneyMarketFundOperation(request.SecId!.Trim(), request.Date, request.Side,
                    request.Quantity, request.Price, request.Commission), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ApplicationConflictException(exception.Message);
        }
    }
}
