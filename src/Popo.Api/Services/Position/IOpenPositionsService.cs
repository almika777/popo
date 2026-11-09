using Popo.Api.Models;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

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
    Task<PositionResponse> Build(List<PortfolioTradeRecord> trades, MoexMarketdata marketdata);
}