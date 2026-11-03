using Microsoft.EntityFrameworkCore;
using Popo.Core.Issuers;

namespace Popo.Storage.Providers;

public sealed class IssuerLogoIssuerProvider(IDbContextFactory<PopoDbContext> contextFactory)
    : IIssuerLogoIssuerProvider
{
    public async Task<IReadOnlyList<IssuerLogoIssuer>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MoexEmitents
            .AsNoTracking()
            .OrderBy(issuer => issuer.Title)
            .Select(issuer => new IssuerLogoIssuer(issuer.Id, issuer.Title))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int emitterId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.MoexEmitents.AnyAsync(issuer => issuer.Id == emitterId, cancellationToken);
    }
}
