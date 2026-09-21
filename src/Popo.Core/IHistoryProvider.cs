namespace Popo.Core;

public interface IHistoryProvider
{
    Task<Dictionary<string, double>> GetMedianDailyVolume(CancellationToken ct);
    Task<Dictionary<string, DailyVolumeStatistics>> GetDailyVolumeStatistics(CancellationToken ct);
}

public sealed record DailyVolumeStatistics(double Average, double Median);
