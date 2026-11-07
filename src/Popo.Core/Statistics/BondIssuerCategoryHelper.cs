namespace Popo.Core.Statistics;

public static class BondIssuerCategoryHelper
{
    public static BondIssuerCategory? GetIssuerCategory(string? moexType) => moexType?.Trim().ToLowerInvariant() switch
    {
        "exchange_bond" or "corporate_bond" => BondIssuerCategory.Corporate,
        "subfederal_bond" or "municipal_bond" => BondIssuerCategory.Municipal,
        "ofz_bond" or "government_bond" => BondIssuerCategory.Government,
        _ => null,
    };
}
