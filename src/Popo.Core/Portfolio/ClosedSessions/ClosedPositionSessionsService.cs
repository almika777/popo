using Popo.Core.Common;

namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class ClosedPositionSessionsService(
    IClosedPositionSessionSourceProvider sourceProvider,
    IClosedPositionSessionsProvider sessionsProvider)
{
    /// <summary>Загружает данные на текущую московскую дату, пересчитывает закрытые сессии и заменяет сохранённый снимок.</summary>
    public async Task<int> RecalculateAsync(CancellationToken cancellationToken)
    {
        var instruments = await sourceProvider.GetAsync(MoscowTime.Today, cancellationToken);
        var calculatedAt = DateTimeOffset.UtcNow;
        var calculator = new ClosedPositionSessionCalculator();
        var sessions = new List<ClosedPositionSession>();
        foreach (var instrument in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sessions.AddRange(calculator.Calculate(instrument, calculatedAt));
        }
        await sessionsProvider.ReplaceAsync(sessions, cancellationToken);
        return sessions.Count;
    }
}
