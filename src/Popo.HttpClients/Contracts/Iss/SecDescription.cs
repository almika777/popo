using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class SecDescription
{
    [JsonPropertyName("SECID")]
    public string SecId { get; set; } = string.Empty;

    [JsonPropertyName("SHORTNAME")]
    public string ShortName { get; set; } = string.Empty;    
    
    [JsonPropertyName("NAME")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ISIN")]
    public string Isin { get; set; } = null!;
    
    [JsonPropertyName("is_traded")]
    public byte IsTraded { get; set; }    
    
    [JsonPropertyName("emitent_id")]
    public long EmitentId { get; set; }    
    
    [JsonPropertyName("emitent_title")]
    public string EmitentTitle { get; set; } = null!;

    [JsonPropertyName("PRIMARY_BOARDID")]
    public string PrimaryBoardId { get; set; } = null!;   
    
    [JsonPropertyName("marketprice_boardid")]
    public string MarketPriceBoardId { get; set; } = null!;
}


