using Popo.Core.PortfolioReturns;

namespace Popo.Storage.Entities;

public sealed class PortfolioValuationEntity
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public double TotalValue { get; set; }

    public string Comment { get; set; } = string.Empty;
}
