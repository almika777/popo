namespace Popo.Core.Initialization;

public enum InitializationJobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public sealed record InitializationJobState(
    string JobKey,
    string DisplayName,
    InitializationJobStatus Status,
    int ProcessedItems,
    int? TotalItems,
    string? Phase,
    int Attempts,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? LastError,
    DateTimeOffset UpdatedAt)
{
    public int? ProgressPercent => TotalItems is > 0
        ? Math.Clamp((int)Math.Round(ProcessedItems * 100d / TotalItems.Value), 0, 100)
        : Status == InitializationJobStatus.Succeeded ? 100 : null;
}

public sealed record InitializationStatus(
    string Version,
    bool IsVisible,
    bool IsComplete,
    IReadOnlyList<InitializationJobState> Jobs);
