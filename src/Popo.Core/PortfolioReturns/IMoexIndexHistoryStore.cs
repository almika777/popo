using Popo.Core.Contracts.Iss;

namespace Popo.Core.PortfolioReturns;

public interface IMoexIndexHistoryStore
{
    Task UpsertAsync(IReadOnlyCollection<MoexIndexHistory> history, CancellationToken cancellationToken);

    Task<IReadOnlyList<MoexIndexHistory>> GetHistoryAsync(
        string secId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
