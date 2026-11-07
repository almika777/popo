namespace Popo.Core;

public interface IHistoryProvider
{
    Task<IDictionary<SecBoard, DateStatistics>> GetStatisticsAsync(DateOnly date, CancellationToken ct);
    Task<Dictionary<string, double>> GetMedianDailyVolume(CancellationToken ct);
    Task<Dictionary<string, DailyVolumeStatistics>> GetDailyVolumeStatistics(CancellationToken ct);
    Task<DateOnly?> GetLatestTradingDateBeforeAsync(DateOnly date, CancellationToken cancellationToken);
}

public sealed record DailyVolumeStatistics(double Average, double Median);
public record struct DateStatistics(double? ClosePrice, double? NumTrades, double? Value);
