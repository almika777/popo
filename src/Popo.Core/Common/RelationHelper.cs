namespace Popo.Core.Common;

public static class RelationHelper
{
    public static readonly Dictionary<string , string> BoardCurrency = new()
    {
        { "TQCB", "RUB" },
        { "TQOY", "CNY" },
        { "TQOB", "RUB" },
        { "TQOD", "USD" },
        { "TQOE", "EUR" },
    };
    public static readonly IReadOnlyDictionary<string, (string BoardId, string Name)> MoneyMarketFunds =
        new Dictionary<string, (string BoardId, string Name)>(StringComparer.OrdinalIgnoreCase)
        {
            ["LQDT"] = ("TQBR", "LQDT"),
            ["TMON"] = ("TQBR", "TMON")
        };
}

