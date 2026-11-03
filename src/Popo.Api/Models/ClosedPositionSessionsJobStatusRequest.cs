using Microsoft.AspNetCore.Mvc;

namespace Popo.Api.Models;

public sealed class ClosedPositionSessionsJobStatusRequest
{
    [FromRoute(Name = "jobId")]
    public string JobId { get; init; } = string.Empty;
}
