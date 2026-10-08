using Microsoft.AspNetCore.Mvc;
using Popo.Api.Services;
using Popo.Core.Initialization;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/initialization")]
public sealed class InitializationController(InitializationService initializationService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<InitializationStatus>> GetStatus(CancellationToken cancellationToken) =>
        Ok(await initializationService.GetStatusAsync(cancellationToken));

    [HttpPost("jobs/{jobKey}/retry")]
    public async Task<ActionResult<InitializationStatus>> RetryJob(
        string jobKey,
        CancellationToken cancellationToken)
        => Ok(await initializationService.RetryJobAsync(jobKey, cancellationToken));
}
