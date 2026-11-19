using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Api.Services.Position;

internal sealed record PositionSourceData(
        IReadOnlyList<PortfolioHoldingRecord> Positions,
        IReadOnlyList<MoexMarketdata> Marketdata,
        IReadOnlyList<MoexBond> Securities);

internal sealed record PositionData(
        PortfolioHoldingRecord Position,
        MoexMarketdata? Marketdata,
        MoexBond? Security);

internal sealed record CurrencyRateSnapshot(DateOnly RateDate, string CurrencyCode, double UnitRate);

internal sealed record PositionMarketPriceData(
        double? TradingCurrencyRateToRub,
        double? MarketPrice);

