namespace Popo.Core.Bonds;

public readonly record struct BondSecurityDto(
    string SecId,
    string Isin,
    string BoardId,
    string? BondType,
    string FaceUnit,
    double FaceValue,
    string? CurrencyId,
    int? EmitterId,
    string? IssuerName,
    int? CouponFrequency,
    DateOnly? SettleDate,
    DateOnly? OfferDate,
    DateOnly? MatDate,
    double? AccruedInterest = null,
    DateOnly? BuybackDate = null,
    double? BuybackPrice = null,
    DateOnly? CallOptionDate = null,
    DateOnly? PutOptionDate = null,
    string? BondSubType = null,
    string? ShortName = null)
{
    public bool IsFloater =>
        IsFloaterType(BondType) || IsFloaterType(BondSubType);

    private static bool IsFloaterType(string? value) =>
        value?.Contains("флоатер", StringComparison.OrdinalIgnoreCase) == true
        || value?.Contains("плавающ", StringComparison.OrdinalIgnoreCase) == true
            && value.Contains("купон", StringComparison.OrdinalIgnoreCase);

    public bool RequiresCouponSchedule =>
        !(BondType?.Contains("дисконт", StringComparison.OrdinalIgnoreCase) == true
          || BondSubType?.Contains("дисконт", StringComparison.OrdinalIgnoreCase) == true);

    public bool RequiresExplicitRedemption =>
        BondType?.Contains("структур", StringComparison.OrdinalIgnoreCase) == true
        || BondSubType?.Contains("структур", StringComparison.OrdinalIgnoreCase) == true
        || BondType?.Contains("конверт", StringComparison.OrdinalIgnoreCase) == true
        || BondSubType?.Contains("конверт", StringComparison.OrdinalIgnoreCase) == true;
}
