using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public sealed class BrokerReportImportProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IBrokerReportImportProvider
{
    public async Task<BrokerReportImportResult> AddAsync(
        BrokerReportImportBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tradeIds = batch.Trades.Select(x => x.Id).ToArray();
        var existingTradeIds = tradeIds.Length == 0
            ? new HashSet<Guid>()
            : await dbContext.PortfolioTrades.Where(x => tradeIds.Contains(x.Id))
                .Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var tradesToAdd = batch.Trades.Where(x => !existingTradeIds.Contains(x.Id)).ToArray();

        var fundOperationIds = batch.FundOperations.Select(x => x.Id).ToArray();
        var existingFundOperationIds = fundOperationIds.Length == 0
            ? new HashSet<Guid>()
            : await dbContext.MoneyMarketFundOperations.Where(x => fundOperationIds.Contains(x.Id))
                .Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var fundOperationsToAdd = batch.FundOperations
            .Where(x => !existingFundOperationIds.Contains(x.Id)).ToArray();

        var cashFlowIds = batch.CashFlows.Select(x => x.Id).ToArray();
        var existingCashFlowIds = cashFlowIds.Length == 0
            ? new HashSet<Guid>()
            : await dbContext.PortfolioCashFlows.Where(x => cashFlowIds.Contains(x.Id))
                .Select(x => x.Id).ToHashSetAsync(cancellationToken);
        var cashFlowsToAdd = batch.CashFlows.Where(x => !existingCashFlowIds.Contains(x.Id)).ToArray();

        var funds = await dbContext.MoneyMarketFunds.AsNoTracking().ToListAsync(cancellationToken);
        var existingFundOperations = await dbContext.MoneyMarketFundOperations.AsNoTracking()
            .ToListAsync(cancellationToken);

        ValidateFundOperations(fundOperationsToAdd.Select(x => x.Operation).ToArray(), funds, existingFundOperations);

        var tradeEntities = tradesToAdd.Select(x => ToEntity(x.Id, x.Operation)).ToArray();
        var fundOperationEntities = fundOperationsToAdd.Select(x => ToEntity(x.Id, x.Operation)).ToArray();
        var cashFlowEntities = cashFlowsToAdd.Select(x => ToEntity(x.Id, x.Operation)).ToArray();

        if (tradeEntities.Length + fundOperationEntities.Length + cashFlowEntities.Length == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new BrokerReportImportResult(0, 0, 0);
        }

        dbContext.PortfolioTrades.AddRange(tradeEntities);
        dbContext.MoneyMarketFundOperations.AddRange(fundOperationEntities);
        dbContext.PortfolioCashFlows.AddRange(cashFlowEntities);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BrokerReportImportResult(
            tradeEntities.Length,
            fundOperationEntities.Length,
            cashFlowEntities.Length);
    }

    private static void ValidateFundOperations(
        IReadOnlyList<MoneyMarketFundOperation> importedOperations,
        IReadOnlyList<MoneyMarketFundEntity> funds,
        IReadOnlyList<MoneyMarketFundOperationEntity> existingOperations)
    {
        foreach (var group in importedOperations.GroupBy(x => x.SecId, StringComparer.OrdinalIgnoreCase))
        {
            var fund = funds.SingleOrDefault(x => string.Equals(x.SecId, group.Key, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Сначала добавьте базовый остаток фонда {group.Key} в разделе «Ликвидность».");

            var operations = existingOperations
                .Where(x => string.Equals(x.SecId, group.Key, StringComparison.OrdinalIgnoreCase))
                .Select(x => new MoneyMarketFundOperation(x.SecId, x.Date, x.Side, x.Quantity, x.Price, x.Commission))
                .Concat(group)
                .ToArray();

            MoneyMarketFundOperationCalculator.CalculatePosition(
                new MoneyMarketFund(fund.SecId, fund.BoardId, fund.Quantity, fund.AveragePrice),
                operations);
        }
    }

    private static PortfolioTradeEntity ToEntity(Guid id, PortfolioTrade trade)
    {
        TradeSettlementCalculator.Calculate(
            trade.Side, trade.Quantity, trade.Price, trade.AccruedInterest, trade.CommissionPercent);

        if (!double.IsFinite(trade.FaceValue) || trade.FaceValue <= 0
            || string.IsNullOrWhiteSpace(trade.SecId)
            || string.IsNullOrWhiteSpace(trade.BoardId)
            || string.IsNullOrWhiteSpace(trade.CurrencyId))
        {
            throw new ArgumentException("Проверьте бумагу, площадку, валюту и номинал импортируемой сделки.");
        }

        return new PortfolioTradeEntity
        {
            Id = id,
            SecId = trade.SecId.Trim().ToUpperInvariant(),
            BoardId = trade.BoardId.Trim().ToUpperInvariant(),
            CurrencyId = trade.CurrencyId.Trim().ToUpperInvariant(),
            TradeDate = trade.TradeDate,
            Side = trade.Side,
            Quantity = trade.Quantity,
            Price = trade.Price,
            FaceValue = trade.FaceValue,
            AccruedInterest = trade.AccruedInterest,
            Commission = trade.CommissionAmount
        };
    }

    private static MoneyMarketFundOperationEntity ToEntity(Guid id, MoneyMarketFundOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.SecId)
            || !double.IsFinite(operation.Quantity) || operation.Quantity <= 0
            || operation.Quantity != Math.Truncate(operation.Quantity)
            || !double.IsFinite(operation.Price) || operation.Price <= 0
            || !double.IsFinite(operation.Commission) || operation.Commission < 0)
        {
            throw new ArgumentException("Проверьте фонд, направление, количество, цену и комиссию.");
        }

        return new MoneyMarketFundOperationEntity
        {
            Id = id,
            SecId = operation.SecId.Trim().ToUpperInvariant(),
            Date = operation.Date,
            Side = operation.Side,
            Quantity = operation.Quantity,
            Price = operation.Price,
            Commission = operation.Commission
        };
    }

    private static PortfolioCashFlowEntity ToEntity(Guid id, PortfolioCashFlowInput cashFlow)
    {
        if (cashFlow.Type is not (PortfolioCashFlowType.Deposit or PortfolioCashFlowType.Withdrawal)
            || !double.IsFinite(cashFlow.Amount) || cashFlow.Amount <= 0)
        {
            throw new ArgumentException("Пополнение или вывод должны иметь положительную сумму в рублях.");
        }

        return new PortfolioCashFlowEntity
        {
            Id = id,
            Date = cashFlow.Date,
            Type = cashFlow.Type,
            Amount = cashFlow.Amount,
            Comment = "Импорт из брокерского отчёта Т-Инвестиции"
        };
    }
}
