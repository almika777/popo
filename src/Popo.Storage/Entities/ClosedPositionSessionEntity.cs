namespace Popo.Storage.Entities;

public sealed class ClosedPositionSessionEntity
{
    public Guid Id { get; set; }
    public string SecId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string CurrencyId { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int HoldingDays { get; set; }
    public decimal BoughtQuantity { get; set; }
    public decimal BoughtCleanAmount { get; set; }
    public decimal SoldCleanAmount { get; set; }
    public decimal Commission { get; set; }
    public decimal? AmortizationIncome { get; set; }
    public decimal? BodyPnl { get; set; }
    public decimal? CouponIncome { get; set; }
    public decimal? TotalPnl { get; set; }
    public decimal? TotalPnlPercent { get; set; }
    public decimal? AnnualizedPnlPercent { get; set; }
    public bool IsComplete { get; set; }
    public string? IncompleteReason { get; set; }
    public string CalculatorKind { get; set; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; set; }
}
