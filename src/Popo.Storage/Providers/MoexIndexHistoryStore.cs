using Microsoft.EntityFrameworkCore;
using Popo.Core.Contracts.Iss;
using Popo.Core.PortfolioReturns;
using Popo.Storage.Entities.Moex;

namespace Popo.Storage.Providers;

public sealed class MoexIndexHistoryStore(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IMoexIndexHistoryStore
{
    public async Task UpsertAsync(
        IReadOnlyCollection<MoexIndexHistory> history,
        CancellationToken cancellationToken)
    {
        if (history.Count == 0)
        {
            return;
        }

        var records = history.Select(ToEntity).ToArray();
        var duplicate = records
            .GroupBy(x => (x.SecId, x.TradeDate))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"MOEX вернул несколько значений для {duplicate.Key.SecId} за {duplicate.Key.TradeDate:yyyy-MM-dd}.",
                nameof(history));
        }

        var secIds = records.Select(x => x.SecId).Distinct().ToArray();
        var from = records.Min(x => x.TradeDate);
        var to = records.Max(x => x.TradeDate);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.MoexIndexHistories
            .AsTracking()
            .Where(x => secIds.Contains(x.SecId) && x.TradeDate >= from && x.TradeDate <= to)
            .ToDictionaryAsync(x => (x.SecId, x.TradeDate), cancellationToken);

        foreach (var record in records)
        {
            if (existing.TryGetValue((record.SecId, record.TradeDate), out var current))
            {
                current.Close = record.Close;
                continue;
            }

            context.MoexIndexHistories.Add(record);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MoexIndexHistory>> GetHistoryAsync(
        string secId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secId);
        if (from > to)
        {
            throw new ArgumentOutOfRangeException(nameof(from), "Начальная дата не должна быть позже конечной.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MoexIndexHistories
            .Where(x => x.SecId == secId && x.TradeDate >= from && x.TradeDate <= to)
            .OrderBy(x => x.TradeDate)
            .Select(x => new MoexIndexHistory
            {
                SecId = x.SecId,
                TradeDate = x.TradeDate,
                Close = x.Close
            })
            .ToListAsync(cancellationToken);
    }

    private static MoexIndexHistoryEntity ToEntity(MoexIndexHistory point)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentException.ThrowIfNullOrWhiteSpace(point.SecId);
        if (point.TradeDate is null)
        {
            throw new ArgumentException("У значения MOEX не указана дата торгов.", nameof(point));
        }

        if (point.Close is null)
        {
            throw new ArgumentException("У значения MOEX не указана расчётная стоимость.", nameof(point));
        }

        return new MoexIndexHistoryEntity
        {
            SecId = point.SecId,
            TradeDate = point.TradeDate.Value,
            Close = point.Close.Value
        };
    }
}
