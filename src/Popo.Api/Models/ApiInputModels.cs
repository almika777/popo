using Microsoft.AspNetCore.Mvc;

namespace Popo.Api.Models;

public sealed class BondSearchRequest
{
    [FromQuery(Name = "q")]
    public string? Query { get; init; }
}

public sealed class PortfolioReturnQuery
{
    [FromQuery(Name = "from")]
    public DateOnly From { get; init; }

    [FromQuery(Name = "to")]
    public DateOnly To { get; init; }

    [FromQuery(Name = "includeLqdt")]
    public bool IncludeLqdt { get; init; } = true;

    [FromQuery(Name = "includeIndexedBond")]
    public bool IncludeIndexedBond { get; init; } = true;
}
