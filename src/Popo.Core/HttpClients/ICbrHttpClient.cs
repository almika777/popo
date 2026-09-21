using Popo.Core.Contracts;

namespace Popo.Core.HttpClients;

public interface ICbrHttpClient
{
    Task<IReadOnlyList<CbrCurrencyDefinitionDto>> GetCurrenciesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CbrCurrencyRateDto>> GetCurrencyRatesAsync(
        CbrCurrencyDefinitionDto currency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
