using Popo.Core.Contracts;

namespace Popo.Core.HttpClients;

public interface ICbondClient
{
    Task<IReadOnlyList<CbondSecurityDto>> GetRatingsAsync(CancellationToken cancellationToken);
}

