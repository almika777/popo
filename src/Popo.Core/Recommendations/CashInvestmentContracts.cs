namespace Popo.Core.Recommendations;

public enum CreditRating
{
    D,
    C,
    CC,
    CCC,
    B_minus,
    B,
    B_plus,
    BB_minus,
    BB,
    BB_plus,
    BBB_minus,
    BBB,
    BBB_plus,
    A_minus,
    A,
    A_plus,
    AA_minus,
    AA,
    AA_plus,
    AAA_minus,
    AAA
}

public enum BondInstrumentType
{
    Any,
    Floating,
    Fixed
}

public sealed record InvestmentStrategySettings(
    CreditRating MinimumRating,
    int? MinimumMaturityDays,
    int? MaximumMaturityDays,
    double MinimumMedianDailyVolume,
    int OfferWindowDays,
    double MaximumYtm = 30d,
    double MinimumYtm = 15d,
    BondInstrumentType InstrumentType = BondInstrumentType.Any,
    string? FaceUnit = null,
    string? CurrencyId = null)
{
    public static InvestmentStrategySettings Defaults => new(
        CreditRating.AA_minus,
        30,
        730,
        1000d,
        90,
        30d,
        15d,
        BondInstrumentType.Any,
        null,
        null);
}
