namespace Popo.Storage.Entities.Moex;

public class SecInfoEntity
{
    public string SecId { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Isin { get; set; } = string.Empty;
    public int? EmitentId { get; set; }
    public string? EmitentTitle { get; set; }
    public string PrimaryBoardId { get; set; } = string.Empty;
    public string? MarketPriceBoardId { get; set; }
}

