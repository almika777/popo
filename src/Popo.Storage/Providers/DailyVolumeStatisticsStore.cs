using Microsoft.EntityFrameworkCore;

namespace Popo.Storage.Providers;

public sealed class DailyVolumeStatisticsStore(IDbContextFactory<PopoDbContext> dbContextFactory)
{
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize publishers while allowing readers to keep reading the committed snapshot.
        await db.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"DailyVolumeStatistics\" IN EXCLUSIVE MODE", cancellationToken);
        await db.DailyVolumeStatistics.ExecuteDeleteAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(RefreshSql, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private const string RefreshSql = """
                                      INSERT INTO "DailyVolumeStatistics" ("SecId", "Average", "Median", "HistoryThroughDate", "CalculatedAt")
                                      WITH daily_volume AS (
                                          SELECT "SecId", "TradeDate", SUM("Volume") AS "Volume"
                                          FROM "MoexHistoryYieldsEntities"
                                          WHERE "Volume" IS NOT NULL
                                          GROUP BY "SecId", "TradeDate"
                                      ), ranked_volume AS (
                                          SELECT "SecId", "TradeDate", "Volume",
                                                 ROW_NUMBER() OVER (PARTITION BY "SecId" ORDER BY "TradeDate" DESC) AS row_number
                                          FROM daily_volume
                                      )
                                      SELECT "SecId", AVG("Volume"),
                                             percentile_cont(0.5) WITHIN GROUP (ORDER BY "Volume"),
                                             MAX("TradeDate"), statement_timestamp()
                                      FROM ranked_volume
                                      WHERE row_number <= 20
                                      GROUP BY "SecId";
                                      """;
}
