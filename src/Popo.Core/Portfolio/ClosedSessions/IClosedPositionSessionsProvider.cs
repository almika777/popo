namespace Popo.Core.Portfolio.ClosedSessions;

public interface IClosedPositionSessionSourceProvider
{
    Task<IReadOnlyList<ClosedSessionInstrument>> GetAsync(DateOnly asOf, CancellationToken cancellationToken);
}

public interface IClosedPositionSessionsProvider
{
    Task<IReadOnlyList<ClosedPositionSession>> GetAsync(CancellationToken cancellationToken);
    Task ReplaceAsync(IReadOnlyList<ClosedPositionSession> sessions, CancellationToken cancellationToken);
}

/// <summary>Общий контракт позволяет API поставить задачу, исполняемую в Popo.Jobs.</summary>
public interface IClosedPositionSessionsJob
{
    Task UpdateAsync(CancellationToken cancellationToken = default);
}
