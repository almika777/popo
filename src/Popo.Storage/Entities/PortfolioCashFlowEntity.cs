using Popo.Core.PortfolioReturns;

namespace Popo.Storage.Entities;

public sealed class PortfolioCashFlowEntity
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public PortfolioCashFlowType Type { get; set; }

    public double Amount { get; set; }

    public string Comment { get; set; } = string.Empty;
}
