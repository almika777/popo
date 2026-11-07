using Microsoft.AspNetCore.Http;

namespace Popo.Api.Models;

public sealed class IssuerLogoUploadRequest
{
    public int EmitterId { get; set; }
    public IFormFile? File { get; set; }
}

public sealed record IssuerLogoListItem(int EmitterId, string Title, bool HasLogo);
