using Popo.Api.Models;
using Popo.Core.Issuers;

namespace Popo.Api.Services;

public sealed class IssuerLogoManagementService(
    IIssuerLogoIssuerProvider issuerProvider,
    IssuerLogoFileStore fileStore)
{
    public async Task<IReadOnlyList<IssuerLogoListItem>> GetIssuersAsync(CancellationToken cancellationToken)
    {
        var issuers = await issuerProvider.GetAllAsync(cancellationToken);
        return issuers
            .Select(issuer => new IssuerLogoListItem(issuer.EmitterId, issuer.Title, fileStore.HasLogo(issuer.EmitterId)))
            .ToArray();
    }

    public async Task<bool> UploadAsync(IssuerLogoUploadRequest request, CancellationToken cancellationToken)
    {
        if (!await issuerProvider.ExistsAsync(request.EmitterId, cancellationToken))
            return false;

        await using var content = request.File!.OpenReadStream();
        await fileStore.SaveAsync(request.EmitterId, Path.GetExtension(request.File.FileName), content, cancellationToken);
        return true;
    }

    public Task<IssuerLogoImage?> GetLogoAsync(int emitterId, CancellationToken cancellationToken) =>
        fileStore.GetAsync(emitterId, cancellationToken);
}
