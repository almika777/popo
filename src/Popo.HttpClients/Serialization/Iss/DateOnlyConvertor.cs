using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Popo.HttpClients.Serialization.Iss;

public class DateOnlyConvertor : JsonConverter<DateOnly?>
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "dd.MM.yyyy",
        "dd.MM.yyyy HH:mm:ss"
    };

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 1. Handle JSON null literal
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
            return ParseStringValue(reader.GetString());

        // MOEX sometimes delivers numbers (like timestamps), handle if necessary
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out long unixTime))
            return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime);

        throw new JsonException($"Неожиданный токен при разборе даты: {reader.TokenType}.");
    }

    private static DateOnly? ParseStringValue(string? value)
    {
        // 2. Handle empty/whitespace strings or MOEX placeholders like "-" or "0000-00-00"
        if (string.IsNullOrWhiteSpace(value) || value == "-" || value.Contains("0000"))
            return null;

        // 3. Try parsing known exact formats
        foreach (var format in Formats)
        {
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;
        }

        // 4. Fallback: Standard DateTime parsing
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            return DateOnly.FromDateTime(dateTime);

        // If we get here, throw an explicit error detailing what failed
        throw new JsonException($"Не удалось преобразовать значение MOEX '{value}' в дату.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }
}
