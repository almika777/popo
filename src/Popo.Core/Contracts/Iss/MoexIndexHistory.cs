namespace Popo.Core.Contracts.Iss;

public sealed class MoexIndexHistory
{
    public DateOnly? TradeDate { get; set; }
    public string? SecId { get; set; }
    public decimal? Close { get; set; }
}
