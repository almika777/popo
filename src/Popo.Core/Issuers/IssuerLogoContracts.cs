namespace Popo.Core.Issuers;

public sealed record IssuerLogoIssuer(int EmitterId, string Title);

public interface IIssuerLogoIssuerProvider
{
    Task<IReadOnlyList<IssuerLogoIssuer>> GetAllAsync(CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int emitterId, CancellationToken cancellationToken);
}
