namespace Popo.Jobs.Initialization;

public interface IInitializationProgressReporter
{
    void Begin(string jobKey);

    void End();

    Task ReportAsync(int processedItems, int? totalItems, string? phase, CancellationToken cancellationToken);
}
