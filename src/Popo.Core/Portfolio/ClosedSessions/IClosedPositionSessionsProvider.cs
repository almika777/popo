namespace Popo.Core.Portfolio.ClosedSessions;

public interface IClosedPositionSessionSourceProvider
{
    /// <summary>Возвращает сделки и связанные данные облигаций, нужные для расчёта на указанную дату.</summary>
    Task<IReadOnlyList<ClosedSessionInstrument>> GetAsync(DateOnly asOf, CancellationToken cancellationToken);
}

public interface IClosedPositionSessionsProvider
{
    Task<IReadOnlyList<ClosedPositionSession>> GetAsync(CancellationToken cancellationToken);

    /// <summary>Заменяет сохранённый снимок закрытых сессий рассчитанным набором.</summary>
    Task ReplaceAsync(IReadOnlyList<ClosedPositionSession> sessions, CancellationToken cancellationToken);
}

/// <summary>Общий контракт позволяет API поставить задачу, исполняемую в Popo.Jobs.</summary>
public interface IClosedPositionSessionsJob
{
    /// <summary>Запускает пересчёт и публикацию закрытых сессий в фоновой задаче.</summary>
    Task UpdateAsync(CancellationToken cancellationToken = default);
}
