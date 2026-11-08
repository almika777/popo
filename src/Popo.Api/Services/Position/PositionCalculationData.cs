using Popo.Core;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Services.Position;

internal sealed record PositionSourceData(
        IReadOnlyList<PortfolioPositionRecord> Positions,
        IReadOnlyList<PortfolioTradeRecord> Trades,
        IReadOnlyList<MoexMarketdata> Marketdata,
        IReadOnlyList<MoexBond> Securities,
        IReadOnlyDictionary<string, DailyVolumeStatistics> VolumeStatistics)
    {
        public static PositionSourceData Empty =>
            new([], [], [], [], new Dictionary<string, DailyVolumeStatistics>(StringComparer.OrdinalIgnoreCase));
    }

internal sealed record PositionData(
        PortfolioPositionRecord Position,
        IReadOnlyList<PortfolioTradeRecord> Trades,
        MoexMarketdata? Marketdata,
        MoexBond? Security,
        DailyVolumeStatistics? VolumeStatistics)
    {
        public bool IsNominalIndexed => Security is not null
                                        && Security.InitialFaceValue > 0
                                        && Security.FaceValue > Security.InitialFaceValue;
    }

internal sealed record PositionAdditionalData(
        IReadOnlyList<PublishedCouponValue> CouponHistory,
        IReadOnlyList<CurrencyRateSnapshot> CurrencyRates);

internal sealed record CurrencyRateSnapshot(DateOnly RateDate, string CurrencyCode, double UnitRate);

internal sealed record PositionMarketPriceData(
        DateOnly QuoteDate,
        double? TradingCurrencyRateToRub,
        double? FaceCurrencyRateToRub,
        double? MarketPrice);

internal sealed record ValuedPosition(PortfolioPositionRecord Position, double? TradingCurrencyRateToRub);

internal sealed record PositionTradePricing(
        PortfolioTradeRecord Trade,
        double? PurchasePricePercent,
        double? PurchasePriceAtCurrentFaceValue);
