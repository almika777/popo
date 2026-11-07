using Microsoft.AspNetCore.Mvc;
using Popo.Core.Statistics;

namespace Popo.Api.Models;

public sealed class BondSearchRequest
{
    [FromQuery(Name = "q")]
    public string? Query { get; init; }
}

public sealed class BondDailyStatisticsRequest
{
    [FromQuery(Name = "date")]
    public DateOnly Date { get; init; }

    [FromQuery(Name = "minimumTrades")]
    public int MinimumTrades { get; init; } = 5;

    [FromQuery(Name = "minimumTurnoverRub")]
    public double MinimumTurnoverRub { get; init; } = 1_000_000;

    [FromQuery(Name = "issuerCategory")]
    public BondIssuerCategory? IssuerCategory { get; init; }
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

    [FromQuery(Name = "includeTmon")]
    public bool IncludeTmon { get; init; }
}
