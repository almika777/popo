using Popo.Api.Models;

namespace Popo.Api.Services.Position.Builders;

public class FloaterOpenPositionBuilder : IOpenPositionBuilder
{
    public PositionResponse Build(OpenPositionBuildInput input)
    {
        return new PositionResponse
        {
            SecId = "null",
            BoardId = "null",
            CurrencyId = "null",
            ShortName = "null"
        };
    }
}
