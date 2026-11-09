using Popo.Api.Models;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Api.Services.Position.Builders;

public class FloaterOpenPositionBuilder : IOpenPositionBuilder
{
    public Task<PositionResponse> Build(List<PortfolioTradeRecord> trades, MoexMarketdata marketdata)
    {
        throw new NotImplementedException();
    }
}