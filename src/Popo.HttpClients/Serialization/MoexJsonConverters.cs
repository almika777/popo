using System.Text.Json;
using System.Text.Json.Serialization;

namespace Popo.HttpClients.Serialization;

public sealed class MoexDateTimeJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Ожидалась строка даты и времени MOEX или null.");

        return MoexDateTimeParser.ParseUtc(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToUniversalTime());
        else
            writer.WriteNullValue();
    }
}

public sealed class MoexDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Ожидалась строка даты MOEX или null.");

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : MoexDateTimeParser.ParseDateOnly(value);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.ToString("yyyy-MM-dd"));
}
