using System.Globalization;
using System.Reflection;

namespace Popo.HttpClients.Common;

public static class MoexMapper
{
    public static T Map<T>(IReadOnlyDictionary<string, string?> fields) where T : new()
    {
        var result = new T();
        foreach (var property in typeof(T).GetProperties())
        {
            var attribute = property.GetCustomAttribute<MoexFieldAttribute>();
            if (attribute is null || !fields.TryGetValue(attribute.Name, out var value)) continue;
            property.SetValue(result, ConvertValue(value, property.PropertyType));
        }
        return result;
    }

    private static object? ConvertValue(string? value, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type == typeof(string)) return value;
        if (type == typeof(bool)) return value switch { "1" => true, "0" => false, _ => bool.Parse(value) };
        if (type == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(DateOnly)) return DateOnly.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
        if (type.IsEnum) return Enum.Parse(type, value, true);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class MoexFieldAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
