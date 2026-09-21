namespace Popo.Core.Initialization;

public sealed record InitializationJobDefinition(string Key, string DisplayName);

public static class InitializationBootstrap
{
    public const string CurrentVersion = "1";

    public static IReadOnlyList<InitializationJobDefinition> Jobs { get; } =
    [
        new("SecUpdateJob", "Активные SEC"),
        new("MoexBondSecuritiesUpdateJob", "Торговые параметры облигаций"),
        new("MoexBondUpdateJob", "Описания облигаций"),
        new("MoexAmortsAndCouponsUpdateJob", "Купоны и амортизации"),
        new("MoexHistoryPricesUpdateJob", "История цен и объёмы"),
        new("MoexEmitentUpdateJob", "Эмитенты"),
        new("BondRatingUpdateJob", "Кредитные рейтинги"),
        new("CbrCurrencyRatesUpdateJob", "Курсы валют ЦБ")
    ];

    public static bool TryGetJob(string key, out InitializationJobDefinition definition)
    {
        definition = Jobs.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }
}
