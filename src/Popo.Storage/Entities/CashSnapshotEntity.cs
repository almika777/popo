namespace Popo.Storage.Entities;

public sealed class CashSnapshotEntity
{
    public Guid Id { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public DateOnly SnapshotDate { get; set; }
    public double Amount { get; set; }
    public string Comment { get; set; } = string.Empty;
}
