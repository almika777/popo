using Popo.Core.Statistics;

namespace Popo.Api.Services;

public sealed record BondDailyPriceComparison(
    DateOnly ComparedWithDate,
    IReadOnlyCollection<BondDailyPricePoint> PreviousPrices,
    IReadOnlyCollection<BondDailyPricePoint> CurrentPrices);
