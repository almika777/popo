using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class ColumnMetadata
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("bytes")]
    public int Bytes { get; set; }

    [JsonPropertyName("max_size")]
    public int MaxSize { get; set; }

    [JsonPropertyName("primary_key")]
    public bool PrimaryKey { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("is_optional")]
    public bool IsOptional { get; set; }
}


