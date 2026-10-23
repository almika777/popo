using Microsoft.EntityFrameworkCore;
using Popo.Core.Portfolio;

namespace Popo.Storage.Providers.Portfolio;

/// <summary>Read-only provider последних сохранённых курсов портфеля.</summary>
public sealed class PortfolioCurrencyRatesProvider(IDbContextFactory<PopoDbContext> dbContextFactory)
    : IPortfolioCurrencyRatesProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PortfolioCurrencyRateRecord>> GetLatestRatesAsync(
        IReadOnlyCollection<string> currencyIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currencyIds);

        var requestedCurrencyIds = currencyIds
            .Where(currencyId => !string.IsNullOrWhiteSpace(currencyId))
            .Select(NormalizeCurrencyId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requestedCurrencyIds.Length == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.CurrencyRates
            .AsNoTracking()
            .Where(rate => rate.RateDate <= asOf && requestedCurrencyIds.Contains(rate.CurrencyCode))
            .GroupBy(rate => rate.CurrencyCode)
            .Select(group => new
            {
                CurrencyId = group.Key,
                RateDate = group.Max(rate => rate.RateDate)
            })
            .Join(
                dbContext.CurrencyRates.AsNoTracking(),
                latest => new { latest.CurrencyId, latest.RateDate },
                rate => new { CurrencyId = rate.CurrencyCode, rate.RateDate },
                (_, rate) => new { rate.CurrencyCode, rate.RateDate, rate.UnitRate })
            .OrderBy(rate => rate.CurrencyCode)
            .Select(rate => new PortfolioCurrencyRateRecord(
                rate.CurrencyCode,
                rate.RateDate,
                rate.UnitRate))
            .ToListAsync(cancellationToken);

        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAvailableCurrencyIdsAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currencyIds = await dbContext.CurrencyRates
            .AsNoTracking()
            .Where(rate => rate.RateDate <= asOf)
            .Select(rate => rate.CurrencyCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        return currencyIds
            .Select(NormalizeCurrencyId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(currencyId => currencyId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeCurrencyId(string currencyId) => currencyId.Trim().ToUpperInvariant();
}
