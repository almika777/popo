using Popo.Api.Models;

namespace Popo.Api.Services.Position;

public interface IOpenPositionsService
{
    Task<PositionsPageResponse> GetAsync(CancellationToken cancellationToken);
}

public interface IPositionBuilderFactory
{
    IOpenPositionBuilder Get(string? bondType);
}

public interface IOpenPositionBuilder
{
    PositionResponse Build(OpenPositionBuildInput input);
}
