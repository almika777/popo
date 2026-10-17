using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio.ClosedSessions;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers.Portfolio;

public sealed class ClosedPositionSessionsProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IClosedPositionSessionsProvider
{
    public async Task<IReadOnlyList<ClosedPositionSession>> GetAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ClosedPositionSessions.AsNoTracking()
            .OrderByDescending(x => x.EndDate).ThenBy(x => x.SecId).ThenBy(x => x.Id)
            .Select(x => new ClosedPositionSession(x.Id, x.SecId, x.BoardId, x.CurrencyId, x.ShortName,
                x.StartDate, x.EndDate, x.HoldingDays, x.BoughtQuantity, x.BoughtCleanAmount,
                x.SoldCleanAmount, x.Commission, x.AmortizationIncome, x.BodyPnl, x.CouponIncome,
                x.TotalPnl, x.TotalPnlPercent, x.AnnualizedPnlPercent, x.IsComplete,
                x.IncompleteReason, x.CalculatorKind, x.CalculatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceAsync(IReadOnlyList<ClosedPositionSession> sessions, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Сериализация публикаций; незавершённая транзакция не видна читателям.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ClosedPositionSessions\" IN EXCLUSIVE MODE", cancellationToken);
        await db.ClosedPositionSessions.ExecuteDeleteAsync(cancellationToken);
        db.ClosedPositionSessions.AddRange(sessions.Select(x => new ClosedPositionSessionEntity
        {
            Id = x.Id, SecId = x.SecId, BoardId = x.BoardId, CurrencyId = x.CurrencyId, ShortName = x.ShortName,
            StartDate = x.StartDate, EndDate = x.EndDate, HoldingDays = x.HoldingDays,
            BoughtQuantity = x.BoughtQuantity, BoughtCleanAmount = x.BoughtCleanAmount,
            SoldCleanAmount = x.SoldCleanAmount, Commission = x.Commission,
            AmortizationIncome = x.AmortizationIncome, BodyPnl = x.BodyPnl, CouponIncome = x.CouponIncome,
            TotalPnl = x.TotalPnl, TotalPnlPercent = x.TotalPnlPercent, AnnualizedPnlPercent = x.AnnualizedPnlPercent,
            IsComplete = x.IsComplete, IncompleteReason = x.IncompleteReason,
            CalculatorKind = x.CalculatorKind, CalculatedAt = x.CalculatedAt
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
