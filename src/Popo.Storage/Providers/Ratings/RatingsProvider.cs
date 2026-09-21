using Microsoft.EntityFrameworkCore;
using Popo.Core.Recommendations;

namespace Popo.Storage.Providers.Ratings;

public class RatingsProvider(IDbContextFactory<PopoDbContext> dbContextFactory) : IRatingsProvider
{
    public async Task<Dictionary<string, RatingsDto>> GetBondRatingsAsync(CancellationToken ct)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await db.BondRatings.ToDictionaryAsync(x => x.Isin, x => new RatingsDto(
            x.Isin,
            (CreditRating?)(int?)x.Acra,
            (CreditRating?)(int?)x.Expert,
            (CreditRating?)(int?)x.NRA,
            (CreditRating?)(int?)x.NKR), cancellationToken: ct);
    }
}