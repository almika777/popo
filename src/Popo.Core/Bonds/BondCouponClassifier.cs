namespace Popo.Core.Bonds;

public static class BondCouponClassifier
{
    /// <summary>Определяет по типу или подтипу MOEX, относится ли облигация к флоатерам.</summary>
    public static bool IsFloater(string? bondType, string? bondSubType) =>
        IsFloaterType(bondType) || IsFloaterType(bondSubType);

    private static bool IsFloaterType(string? value) =>
        value?.Contains("флоатер", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("плавающ", StringComparison.OrdinalIgnoreCase) == true
        && value.Contains("купон", StringComparison.OrdinalIgnoreCase);
}
