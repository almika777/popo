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
    public double BoughtQuantity { get; set; }
    public double BoughtCleanAmount { get; set; }
    public double SoldCleanAmount { get; set; }
    public double Commission { get; set; }
    public double? AmortizationIncome { get; set; }
    public double? BodyPnl { get; set; }
    public double? CouponIncome { get; set; }
    public double? TotalPnl { get; set; }
    public double? TotalPnlPercent { get; set; }
    public double? AnnualizedPnlPercent { get; set; }
    public bool IsComplete { get; set; }
    public string? IncompleteReason { get; set; }
    public string CalculatorKind { get; set; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; set; }
}
