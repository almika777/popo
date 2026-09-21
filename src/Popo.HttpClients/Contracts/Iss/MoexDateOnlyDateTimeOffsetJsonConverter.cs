using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Popo.HttpClients.Contracts.Iss;

public sealed class MoexDateOnlyDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    private const string DateOnlyFormat = "yyyy-MM-dd";

    public static bool TryParse(string? raw, out DateTimeOffset? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (DateOnly.TryParseExact(raw, DateOnlyFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            value = new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return true;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset))
        {
            value = dateTimeOffset;
            return true;
        }

        return false;
    }

    public static DateTimeOffset? Parse(string? raw)
    {
        if (TryParse(raw, out var value))
            return value;

        throw new FormatException($"Не удалось разобрать дату MOEX: '{raw}'.");
    }

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Для значения {nameof(DateTimeOffset)} ожидалась строка или null.");

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToUniversalTime());
    }
}


