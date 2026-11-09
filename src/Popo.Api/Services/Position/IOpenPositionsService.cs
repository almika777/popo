using Popo.Api.Models;

namespace Popo.Api.Services.Position;

public interface IOpenPositionsService
{
    Task<PositionsPageResponse> GetAsync(CancellationToken cancellationToken);
}
