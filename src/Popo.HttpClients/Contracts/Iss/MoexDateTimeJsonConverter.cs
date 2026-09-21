using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Popo.Core.Common;

namespace Popo.HttpClients.Contracts.Iss;

public sealed class MoexDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public static bool TryParse(string? raw, out DateTime? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (DateTime.TryParseExact(raw, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            value = MoscowTime.ToUtc(result).UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result))
        {
            value = MoscowTime.ToUtc(result).UtcDateTime;
            return true;
        }

        return false;
    }

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Для значения DateTime ожидалась строка или null.");

        var raw = reader.GetString();
        if (TryParse(raw, out var value))
            return value;

        throw new JsonException($"Не удалось разобрать дату и время MOEX: '{raw}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(DateTimeFormat));
    }
}


