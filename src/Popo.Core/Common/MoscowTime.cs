namespace Popo.Core.Common;

public static class MoscowTime
{
    private static readonly Lazy<TimeZoneInfo> MoscowTimeZone = new(() =>
        OperatingSystem.IsWindows()
            ? TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time")
            : TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

    public static TimeZoneInfo Zone => MoscowTimeZone.Value;

    public static DateTimeOffset Now => ToMoscow(DateTimeOffset.UtcNow);
    public static DateOnly Today => DateOnly.FromDateTime(ToMoscow(DateTimeOffset.UtcNow).DateTime);
    public static DateTimeOffset AddTimeToToday(TimeSpan time) => new DateTimeOffset(Now.Date).Add(time);

    public static DateTimeOffset ToUtc(DateTime localMoscowTime)
    {
        var unspecified = DateTime.SpecifyKind(localMoscowTime, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, Zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public static DateTimeOffset ToUtc(DateTimeOffset value) => value.ToUniversalTime();

    public static DateTimeOffset ToMoscow(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Zone);
    
    public static DateTimeOffset ToMoscow(DateTime value) =>
        ToMoscow(new DateTimeOffset(value, TimeSpan.Zero));

    public static DateTimeOffset MoscowMidnightToUtc(DateOnly value) =>
        ToUtc(value.ToDateTime(TimeOnly.MinValue));

    public static DateTime AtMoscowDateTime(DateOnly date, TimeSpan time) =>
        date.ToDateTime(TimeOnly.FromTimeSpan(time));

    public static DateTime AtMoscowDateTime(DateOnly date, DateTime utcNow)
    {
        var moscowNow = ToMoscow(new DateTimeOffset(utcNow, TimeSpan.Zero));
        return date.ToDateTime(TimeOnly.FromDateTime(moscowNow.DateTime));
    }
}


