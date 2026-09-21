using System.Text.Json;
using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public class MoexResponse<T>
{
    [JsonPropertyName("charsetinfo")] 
    public object CharsetInfo { get; set; } = null!;

    [JsonPropertyName("metadata")] 
    public Dictionary<string, ColumnMetadata> Metadata { get; set; } = null!;

    [JsonPropertyName("columns")] 
    public List<string>? Columns { get; set; }

    [JsonPropertyName("data")] 
    public List<List<JsonElement?>>? Data { get; set; }
    
    public IEnumerable<T> GetTypedData(JsonSerializerOptions options)
    {
        if (Data == null || Columns == null)
            yield break;

        var columnMap = Columns
            .Select((col, i) => new { Name = col.ToUpperInvariant(), Index = i })
            .ToDictionary(x => x.Name, x => x.Index);

        var properties = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToList();

        var propIndexMap = properties
            .Select(prop =>
            {
                var attr = (JsonPropertyNameAttribute?)Attribute.GetCustomAttribute(prop, typeof(JsonPropertyNameAttribute));
                var propName = attr?.Name.ToUpperInvariant() ?? prop.Name.ToUpperInvariant();
                return new { prop, Index = columnMap.GetValueOrDefault(propName, -1) };
            })
            .Where(x => x.Index >= 0)
            .ToList();

        foreach (var row in Data)
        {
            var item = Activator.CreateInstance<T>();
            foreach (var t in propIndexMap)
            {
                var prop = t.prop;
                var index = t.Index;

                if (index >= row.Count)
                    continue;

                var jsonElement = row[index];
                if (!jsonElement.HasValue || jsonElement.Value.ValueKind == JsonValueKind.Null)
                    continue;

                object? value;
                if ((prop.PropertyType == typeof(DateTimeOffset) || prop.PropertyType == typeof(DateTimeOffset?))
                    && jsonElement.Value.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.Value.GetString();
                    if (string.IsNullOrWhiteSpace(str) || str.Contains("0000"))
                    {
                        value = null;
                    }
                    else
                    {
                        if (str.Contains(':')
                            && MoexDateTimeJsonConverter.TryParse(str, out var parsedDateTime)
                            && parsedDateTime.HasValue)
                        {
                            value = new DateTimeOffset(parsedDateTime.Value);
                        }
                        else
                        {
                            value = MoexDateOnlyDateTimeOffsetJsonConverter.TryParse(str, out var parsedDate)
                                ? parsedDate
                                : null;
                        }
                    }
                }
                else if ((prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                    && jsonElement.Value.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.Value.GetString();
                    value = MoexDateTimeJsonConverter.TryParse(str, out var parsedDate) ? parsedDate : null;
                }
                else
                {
                    value = jsonElement.Value.Deserialize(prop.PropertyType, options);
                }

                prop.SetValue(item, value);
            }

            yield return item;
        }
    }
}



