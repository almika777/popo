namespace Popo.Api.Services;

public interface IBondDailyPriceComparisonCalculator
{
    Task<BondDailyPriceComparison?> CalculateAsync(DateOnly date, CancellationToken cancellationToken);
}
