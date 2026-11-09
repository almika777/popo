namespace Popo.Api.Models;

public sealed record PositionsPageResponse(
    IReadOnlyList<PositionResponse> Positions,
    PositionTotalsResponse Totals);

public sealed record PositionTotalsResponse(
    double MarketValueRub,
    double InvestedAmountRub,
    double UnrealizedPnlRub,
    double UnrealizedPnlPercent);

public sealed record PositionResponse
{
    public required string SecId { get; init; }
    public required string BoardId { get; init; }
    public required string CurrencyId { get; init; }
    public required string ShortName { get; init; }
    public int Quantity { get; init; }
    public double UnrealizedPnl { get; init; }
    public double TotalPnl { get; init; }
}