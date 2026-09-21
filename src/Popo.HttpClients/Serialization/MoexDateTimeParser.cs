using System.Globalization;

namespace Popo.HttpClients.Serialization;

/// <summary>Правила разбора дат MOEX: datetime без offset приходит в московском времени.</summary>
public static class MoexDateTimeParser
{
    private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow");

    public static DateTimeOffset ParseUtc(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("Дата и время MOEX не заполнены.");

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out var withOffset) && HasExplicitOffset(value))
            return withOffset.ToUniversalTime();

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces,
                out var local))
            throw new FormatException($"Не удалось разобрать дату и время MOEX: '{value}'.");

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, Moscow), TimeSpan.Zero);
    }

    public static DateOnly ParseDateOnly(string value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new FormatException($"Не удалось разобрать дату MOEX: '{value}'.");

        return date;
    }

    private static bool HasExplicitOffset(string value) =>
        value.EndsWith('Z') || value.Contains('+') ||
        (value.LastIndexOf('-') > value.IndexOf('T') && value.LastIndexOf('-') > 9);
}
