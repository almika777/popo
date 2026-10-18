namespace Popo.Core.PortfolioReturns;

public interface IPortfolioCashFlowsProvider
{
    /// <summary>Возвращает денежные потоки, используемые при восстановлении рублёвого остатка после снимка.</summary>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Сохранённые пополнения, снятия и налоговые списания.</returns>
    Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(CancellationToken cancellationToken);

    Task<PortfolioCashFlowRecord?> GetCashFlowAsync(Guid id, CancellationToken cancellationToken);

    Task<PortfolioCashFlowRecord> AddCashFlowAsync(
        DateOnly date,
        PortfolioCashFlowType type,
        double amount,
        string comment,
        CancellationToken cancellationToken);

    Task<bool> UpdateCashFlowAsync(
        Guid id,
        DateOnly date,
        PortfolioCashFlowType type,
        double amount,
        string comment,
        CancellationToken cancellationToken);

    Task<bool> DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken);
}
