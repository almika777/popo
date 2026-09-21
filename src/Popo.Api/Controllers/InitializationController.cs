using Microsoft.AspNetCore.Mvc;
using Popo.Core.Initialization;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/initialization")]
public sealed class InitializationController(IInitializationStateStore stateStore) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<InitializationStatus>> GetStatus(CancellationToken cancellationToken) =>
        Ok(await stateStore.GetStatusAsync(cancellationToken));

    [HttpPost("jobs/{jobKey}/retry")]
    public async Task<ActionResult<InitializationStatus>> RetryJob(
        string jobKey,
        CancellationToken cancellationToken)
    {
        if (!InitializationBootstrap.TryGetJob(jobKey, out _))
        {
            return NotFound();
        }

        var status = await stateStore.RetryJobAsync(jobKey, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        if (status != InitializationJobStatus.Pending)
        {
            return Conflict(new { status });
        }

        return Ok(await stateStore.GetStatusAsync(cancellationToken));
    }
}
