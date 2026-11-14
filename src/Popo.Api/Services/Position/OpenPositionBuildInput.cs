using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Api.Services.Position;

public sealed record OpenPositionBuildInput
{
    public required IReadOnlyList<PortfolioTradeRecord> Trades { get; init; }
    public required MoexMarketdata Marketdata { get; init; }
    public required MoexBond Security { get; init; }
    public required DateOnly AsOf { get; init; }
    public IReadOnlyList<OpenPositionCoupon> Coupons { get; init; } = [];
    public PortfolioCurrencyRateRecord? CurrentCurrencyRate { get; init; }
}
