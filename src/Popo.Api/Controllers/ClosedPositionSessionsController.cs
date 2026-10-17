using Microsoft.AspNetCore.Mvc;
using Popo.Api.Services.ClosedPositions;
using Popo.Core.Portfolio.ClosedSessions;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/closed-position-sessions")]
public sealed class ClosedPositionSessionsController(ClosedPositionSessionsPageService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClosedPositionSession>>> Get(CancellationToken cancellationToken)
        => Ok(await service.GetAsync(cancellationToken));

    [HttpPost("recalculate")]
    public async Task<ActionResult<ClosedPositionSessionsJobResponse>> Recalculate(CancellationToken cancellationToken)
        => Accepted(await service.RecalculateAsync(cancellationToken));

    [HttpGet("recalculation/{jobId}")]
    public ActionResult<ClosedPositionSessionsJobStatus> GetStatus(string jobId)
        => Ok(service.GetStatus(jobId));
}
