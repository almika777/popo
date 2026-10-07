using Popo.Core.Portfolio;

namespace Popo.Storage.Entities;

public sealed class PortfolioTradeEntity
{
    public Guid Id { get; set; }
    public string SecId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string CurrencyId { get; set; } = string.Empty;
    public DateOnly TradeDate { get; set; }
    public TradeSide Side { get; set; }
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double FaceValue { get; set; }
    public double AccruedInterest { get; set; }
    public double Commission { get; set; }
}
