using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/issuer-logos")]
public sealed class IssuerLogosController(IssuerLogoManagementService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IssuerLogoListItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IssuerLogoListItem>>> GetIssuers(CancellationToken cancellationToken) =>
        Ok(await service.GetIssuersAsync(cancellationToken));

    [HttpPut]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IssuerLogoUploadRequest request, CancellationToken cancellationToken) =>
        await service.UploadAsync(request, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{emitterId:int}/image")]
    public async Task<IActionResult> GetLogo(int emitterId, CancellationToken cancellationToken)
    {
        var image = await service.GetLogoAsync(emitterId, cancellationToken);
        if (image is null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        return File(image.Content, image.ContentType);
    }
}
