using System.Net.Http.Json;
using System.Text.Json;
using Popo.Core.Contracts;
using Popo.Core.HttpClients;

namespace Popo.HttpClients;

public sealed class CbondClient(HttpClient httpClient) : ICbondClient
{
    public async Task<IReadOnlyList<CbondSecurityDto>> GetRatingsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("screener/bonds", new { }, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CbondSecurityDto>>(
                   new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
               ?? [];
    }
}

