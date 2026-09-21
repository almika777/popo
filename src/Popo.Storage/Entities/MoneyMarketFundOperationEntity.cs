using Popo.Core.Portfolio;

namespace Popo.Storage.Entities;

public sealed class MoneyMarketFundOperationEntity
{
    public Guid Id { get; set; }
    public string SecId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TradeSide Side { get; set; }
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Commission { get; set; }
}
