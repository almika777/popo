namespace Popo.Core.Contracts.Iss;

public class SecDescription
{
    public string SecId { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;    
    public string Name { get; set; } = string.Empty;
    public string Isin { get; set; } = null!;
    public byte IsTraded { get; set; }    
    public long EmitentId { get; set; }    
    public string EmitentTitle { get; set; } = null!;
    public string PrimaryBoardId { get; set; } = null!;   
    public string MarketPriceBoardId { get; set; } = null!;
}


