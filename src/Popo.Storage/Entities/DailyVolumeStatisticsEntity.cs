namespace Popo.Storage.Entities;

public sealed class DailyVolumeStatisticsEntity
{
    public string SecId { get; set; } = null!;
    public double Average { get; set; }
    public double Median { get; set; }
    public DateOnly HistoryThroughDate { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
}
