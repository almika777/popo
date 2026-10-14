namespace Popo.Storage.Entities.Moex;

public sealed class MoexIndexHistoryEntity
{
    public string SecId { get; set; } = null!;
    public DateOnly TradeDate { get; set; }
    public decimal Close { get; set; }
}
