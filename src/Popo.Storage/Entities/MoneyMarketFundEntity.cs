namespace Popo.Storage.Entities;

public sealed class MoneyMarketFundEntity
{
    public Guid Id { get; set; }
    public string SecId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double AveragePrice { get; set; }
}
