using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexEmitentDescription
{
    [JsonPropertyName("secid")] 
    public string SecId { get; set; } = string.Empty;

    [JsonPropertyName("emitent_id")] 
    public int Id { get; set; }

    [JsonPropertyName("emitent_title")] 
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("emitent_inn")] 
    public string Inn { get; set; } = string.Empty;    
    
    [JsonPropertyName("emitent_okpo")] 
    public string Okpo { get; set; } = string.Empty;   
    
    [JsonPropertyName("is_traded")] 
    public byte IsTraded { get; set; }
}


