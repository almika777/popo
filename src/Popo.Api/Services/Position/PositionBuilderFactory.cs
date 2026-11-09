using Popo.Api.Services.Position.Builders;

namespace Popo.Api.Services.Position;

public class PositionBuilderFactory : IPositionBuilderFactory
{
    public IOpenPositionBuilder Get(string? bondType) => bondType switch
    {
        "Амортизируемая облигация" or "Амортизируемые облигации" => new DefaultOpenPositionBuilder(),
        "Фикс с неизвестным купоном" => new DefaultOpenPositionBuilder(),
        "Облигация с фиксированным (неизвестным) купоном" => new DefaultOpenPositionBuilder(),
        "Облигация с фиксированным (известным) купоном" => new DefaultOpenPositionBuilder(),
        "Дисконтная облигация" => new DefaultOpenPositionBuilder(),
        "Линкер/облигация с индексируемым номиналом" => new DefaultOpenPositionBuilder(),
        "Валютная облигация" or "Валютные облигации" => new DefaultOpenPositionBuilder(),
        "Фикс с известным купоном" => new DefaultOpenPositionBuilder(),
        "Структурная облигация" => new DefaultOpenPositionBuilder(),
        "Конвертируемая облигация" => new DefaultOpenPositionBuilder(),
        "Флоатер" or "Облигация с плавающим купоном" => new FloaterOpenPositionBuilder(),
        _ => throw new ArgumentOutOfRangeException(nameof(bondType), bondType, null)
    };
}