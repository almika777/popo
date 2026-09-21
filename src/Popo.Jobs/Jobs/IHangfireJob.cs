namespace Popo.Jobs.Jobs;

public interface IHangfireJob
{
    Task UpdateAsync(CancellationToken ct = default);
}
