using Popo.Core.Initialization;

namespace Popo.Storage.Entities;

public sealed class InitializationJobStateEntity
{
    public string BootstrapVersion { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public InitializationJobStatus Status { get; set; }
    public int ProcessedItems { get; set; }
    public int? TotalItems { get; set; }
    public string? Phase { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
