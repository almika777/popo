using Popo.Api.Services.Position.Builders;

namespace Popo.Api.Services.Position;

public class PositionBuilderFactory : IPositionBuilderFactory
{
    public IOpenPositionBuilder Get(string? bondType) => bondType switch
    {
        "Облигация с фиксированным (известным) купоном" => new DefaultOpenPositionBuilder(),
        "Фикс с известным купоном" => new DefaultOpenPositionBuilder(),
        "Флоатер" or "Облигация с плавающим купоном" => new FloaterOpenPositionBuilder(),
        _ => new DefaultOpenPositionBuilder()
    };
}
