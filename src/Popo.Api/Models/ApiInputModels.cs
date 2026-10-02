using Microsoft.AspNetCore.Mvc;

namespace Popo.Api.Models;

public sealed class BondSearchRequest
{
    [FromQuery(Name = "q")]
    public string? Query { get; init; }
}

public sealed class BrokerReportPreviewRequest
{
    public const long MaximumFileSizeBytes = 20 * 1024 * 1024;

    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}

public sealed class PortfolioReturnQuery
{
    [FromQuery(Name = "from")]
    public DateOnly From { get; init; }

    [FromQuery(Name = "to")]
    public DateOnly To { get; init; }
}
