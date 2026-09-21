using Popo.Core.Enums;

namespace Popo.Core.Recommendations;

public record struct RatingsDto(
    string Isin,
    CreditRating? Acra,
    CreditRating? Expert,
    CreditRating? Nra,
    CreditRating? Nkr);