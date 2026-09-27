namespace Popo.Core.Portfolio;

public sealed record CashSnapshot(
    string CurrencyId,
    DateOnly SnapshotDate,
    double Amount);

public sealed record CashBalance(
    string CurrencyId,
    double Amount);

public sealed record CashSnapshotRecord(
    Guid Id,
    string CurrencyId,
    DateOnly SnapshotDate,
    double Amount,
    string Comment);

public interface IPortfolioCashProvider
{
    Task<IReadOnlyList<CashSnapshotRecord>> GetCashSnapshotsAsync(CancellationToken cancellationToken);
    Task<CashSnapshotRecord?> GetCashSnapshotAsync(Guid id, CancellationToken cancellationToken);
    Task<CashSnapshotRecord> AddCashSnapshotAsync(CashSnapshot snapshot, string comment, CancellationToken cancellationToken);
    Task<CashSnapshotRecord?> UpdateCashSnapshotAsync(Guid id, CashSnapshot snapshot, string comment, CancellationToken cancellationToken);
    Task<bool> DeleteCashSnapshotAsync(Guid id, CancellationToken cancellationToken);
}
