using Microsoft.EntityFrameworkCore;
using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;
using Popo.Core.Recommendations;
using Popo.Storage;

namespace Popo.Api.Services.Position;

public interface IPortfolioPositionsService
{
    Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken);
    Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken);
}

public sealed class PortfolioPositionsService(
    IPortfolioService portfolioService,
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    IPortfolioTradesProvider tradesProvider,
    IPortfolioPositionsProvider positionsProvider,
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    IHistoryProvider historyProvider,
    IPortfolioCouponProvider couponProvider,
    IPositionRecommendationStore recommendationStore) : IPortfolioPositionsService
{
    public async Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken)
    {
        var recommendationTask = recommendationStore.GetAsync(cancellationToken);
        var valuedPositionsTask = LoadPositionsAsync(cancellationToken);
        await Task.WhenAll(recommendationTask, valuedPositionsTask);

        var valuedPositions = await valuedPositionsTask;
        var positions = valuedPositions.Select(x => x.Position).ToArray();
        var marketValueRub = valuedPositions.Sum(x =>
            x.Position.MarketValue.GetValueOrDefault() * x.TradingCurrencyRateToRub.GetValueOrDefault());
        var unrealizedPnlRub = valuedPositions.Sum(x =>
            x.Position.UnrealizedPnl.GetValueOrDefault() * x.TradingCurrencyRateToRub.GetValueOrDefault());
        var investedAmountRub = marketValueRub - unrealizedPnlRub;
        var totals = new PositionTotalsResponse(
            marketValueRub,
            investedAmountRub,
            unrealizedPnlRub,
            investedAmountRub == 0 ? 0 : unrealizedPnlRub / investedAmountRub * 100);

        return new PositionsPageResponse(
            positions,
            totals,
            PositionRecommendationApiMapper.ToResponse(await recommendationTask));
    }

    public async Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var valuedPositions = await LoadPositionsAsync(cancellationToken);
        return valuedPositions.Select(x => x.Position).ToArray();
    }

    public async Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var valuedPositions = await LoadPositionsAsync(cancellationToken);
        var cashBalances = await portfolioService.GetCashBalancesAsync(asOf, cancellationToken);
        var cashCurrencies = cashBalances
            .Select(x => x.CurrencyId)
            .Where(x => !IsRubleCurrency(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var cashRates = cashCurrencies.Length == 0
            ? []
            : await LoadCurrencyRatesAsync(cashCurrencies, cancellationToken);

        var cashValueRub = cashBalances.Sum(balance =>
            balance.Amount * (GetCurrencyRate(cashRates, balance.CurrencyId, asOf) ?? 0));
        var positionsValueRub = valuedPositions.Sum(x =>
            x.Position.MarketValue.GetValueOrDefault() * x.TradingCurrencyRateToRub.GetValueOrDefault());
        var unrealizedPnlRub = valuedPositions.Sum(x =>
            x.Position.UnrealizedPnl.GetValueOrDefault() * x.TradingCurrencyRateToRub.GetValueOrDefault());
        var funds = await GetMoneyMarketFundsAsync(cancellationToken);
        var fundsValueRub = funds.Sum(x => x.CurrentValue.GetValueOrDefault());

        var summary = new PortfolioSummaryRecord(
            asOf,
            positionsValueRub + cashValueRub + fundsValueRub,
            positionsValueRub,
            cashValueRub,
            fundsValueRub,
            unrealizedPnlRub);
        return new PortfolioOverview(cashBalances, summary);
    }

    public async Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(
        CancellationToken cancellationToken)
    {
        var funds = await moneyMarketFundsProvider.GetMoneyMarketFundsAsync(cancellationToken);
        return await Task.WhenAll(funds.Select(async fund =>
        {
            try
            {
                var quote = await moexHttpClient.GetMoneyMarketFundMarketdataAsync(fund.SecId, fund.BoardId,
                    cancellationToken);
                var currentPrice = quote.CurrentPrice;
                double? currentValue = currentPrice.HasValue ? fund.Quantity * currentPrice.Value : null;
                var invested = fund.Quantity * fund.AveragePrice;
                var pnl = currentValue - invested;
                return fund with
                {
                    CurrentPrice = currentPrice,
                    CurrentValue = currentValue,
                    Pnl = pnl,
                    PnlPercent = invested == 0 ? null : pnl / invested * 100,
                    QuoteTime = quote.SysTime
                };
            }
            catch (HttpRequestException)
            {
                return fund;
            }
            catch (InvalidOperationException)
            {
                return fund;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return fund;
            }
        }));
    }

    private async Task<IReadOnlyList<ValuedPosition>> LoadPositionsAsync(CancellationToken cancellationToken)
    {
        var asOf = MoscowTime.Today;
        var sourceData = await LoadPositionSourceDataAsync(asOf, cancellationToken);
        if (sourceData.Positions.Count == 0)
            return [];

        var positionDatas = ResolvePositionData(sourceData);
        var positionAdditionalData = await LoadPositionAdditionalData(positionDatas, asOf, cancellationToken);

        return positionDatas
            .Select(selection => BuildPosition(selection, sourceData, positionAdditionalData, asOf))
            .ToArray();
    }

    private async Task<PositionSourceData> LoadPositionSourceDataAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var positions = await positionsProvider.GetPositionsAsync(asOf, cancellationToken);
        if (positions.Count == 0)
            return PositionSourceData.Empty;

        var tradesTask = tradesProvider.GetTradesAsync(cancellationToken);
        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        var volumeStatisticsTask = historyProvider.GetDailyVolumeStatistics(cancellationToken);
        await Task.WhenAll(tradesTask, marketdataTask, securitiesTask, volumeStatisticsTask);

        var trades = (await tradesTask).Where(x => x.TradeDate <= asOf).ToArray();
        var marketdata = (await marketdataTask).SelectMany(x => x.Value).ToArray();
        var securities = (await securitiesTask).SelectMany(x => x.Value).ToArray();
        var volumeStatistics = await volumeStatisticsTask;

        return new PositionSourceData(positions, trades, marketdata, securities, volumeStatistics);
    }

    private static IReadOnlyList<PositionData> ResolvePositionData(PositionSourceData sourceData)
    {
        var marketdataByKey = sourceData.Marketdata
            .GroupBy(x => InstrumentKey(x.SecId, x.BoardId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var marketdataBySecId = sourceData.Marketdata
            .GroupBy(x => x.SecId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var securitiesByKey = sourceData.Securities
            .GroupBy(x => InstrumentKey(x.SecId, x.BoardId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        return sourceData.Positions.Select(position =>
        {
            var marketdata = GetMarketdataValue(marketdataByKey, marketdataBySecId, position);
            var securityKey = marketdata is not null
                ? InstrumentKey(marketdata.SecId, marketdata.BoardId)
                : InstrumentKey(position.SecId, position.BoardId);
            var security =  securitiesByKey.GetValueOrDefault(securityKey);
            return new PositionData(
                position,
                marketdata,
                security,
                sourceData.VolumeStatistics.GetValueOrDefault(position.SecId));
        }).ToArray();
    }

    private async Task<PositionAdditionalData> LoadPositionAdditionalData(
        IReadOnlyCollection<PositionData> selections,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var bondKeys = selections
            .Where(x => x.Security is not null)
            .Select(x => new PortfolioBondKey(x.Security!.SecId, x.Security.Isin, x.Security.BoardId))
            .Distinct()
            .ToArray();
        var couponHistory = await couponProvider
            .GetLatestPublishedAsync(bondKeys, asOf, cancellationToken);
        var rateCurrencies = selections
            .Where(x => x.Security is not null)
            .SelectMany(x => new[] { x.Security!.FaceUnit, x.Position.CurrencyId })
            .Where(x => !IsRubleCurrency(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = rateCurrencies.Length == 0
            ? []
            : await LoadCurrencyRatesAsync(rateCurrencies, cancellationToken);

        return new PositionAdditionalData(couponHistory, rates);
    }

    private ValuedPosition BuildPosition(
        PositionData data,
        PositionSourceData sourceData,
        PositionAdditionalData referenceData,
        DateOnly asOf)
    {
        var marketPricing = CalculateMarketPrice(data, referenceData.CurrencyRates, asOf);
        var couponValue = GetPositionCouponValue(data, referenceData, marketPricing.QuoteDate);
        var tradePricing = CalculatePositionTradePricing(
            data,
            sourceData.Trades,
            referenceData.CurrencyRates,
            marketPricing);
        var isNominalIndexed = data.IsNominalIndexed
                               || data.Security is { InitialFaceValue: <= 0, FaceValue: > 0 } security
                               && HasOpenLotNominalGrowth(tradePricing, data.Position.Quantity, security.FaceValue);
        var income = CalculatePositionIncome(data, tradePricing, marketPricing, couponValue, isNominalIndexed, asOf);
        double? averageBuyPriceAtCurrentFaceValue = income is { RemainingQuantity: > 0 }
            ? income.RemainingCleanCost / income.RemainingQuantity
            : null;
        var valuation = PortfolioPositionCalculator.Calculate(
            data.Position,
            marketPricing.MarketPrice,
            averageBuyPriceAtCurrentFaceValue);

        return CreateValuedPosition(
            data,
            marketPricing,
            couponValue,
            income,
            averageBuyPriceAtCurrentFaceValue,
            valuation,
            isNominalIndexed);
    }

    private static bool HasOpenLotNominalGrowth(
        IReadOnlyCollection<PositionTradePricing> tradePricing,
        double remainingQuantity,
        double currentFaceValue)
    {
        var hasNominalGrowth = false;
        foreach (var buy in tradePricing
                     .Where(x => x.Trade.Side == TradeSide.Buy)
                     .OrderBy(x => x.Trade.TradeDate)
                     .Reverse())
        {
            if (remainingQuantity <= 0)
                break;

            var tradeFaceValue = buy.Trade.FaceValue;
            if (!double.IsFinite(tradeFaceValue) || tradeFaceValue <= 0 || tradeFaceValue > currentFaceValue)
                return false;

            hasNominalGrowth |= tradeFaceValue < currentFaceValue;
            remainingQuantity -= buy.Trade.Quantity;
        }

        return remainingQuantity <= 0 && hasNominalGrowth;
    }

    private PositionMarketPriceData CalculateMarketPrice(
        PositionData data,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        DateOnly asOf)
    {
        var quoteDate = data.Marketdata?.SysTime is { } sysTime
            ? DateOnly.FromDateTime(sysTime)
            : asOf;
        var tradingCurrencyRateToRub = GetCurrencyRate(rates, data.Position.CurrencyId, quoteDate);
        var faceCurrencyRateToRub = data.Security is null
            ? null
            : GetCurrencyRate(rates, data.Security.FaceUnit, quoteDate);
        var marketPrice = data.Security is null || data.Marketdata is null
            ? null
            : PortfolioMarketPriceCalculator.Calculate(
                data.Security.FaceValue,
                data.Security.FaceUnit,
                data.Position.CurrencyId,
                data.Marketdata.CurrentPrice,
                faceCurrencyRateToRub,
                tradingCurrencyRateToRub);

        return new PositionMarketPriceData(
            quoteDate,
            tradingCurrencyRateToRub,
            faceCurrencyRateToRub,
            marketPrice);
    }

    private double? GetPositionCouponValue(
        PositionData data,
        PositionAdditionalData referenceData,
        DateOnly quoteDate)
    {
        if (data.Security is null)
            return null;

        var latestCoupon = referenceData.CouponHistory.FirstOrDefault(x =>
            string.Equals(x.SecId, data.Security.SecId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Isin, data.Security.Isin, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.BoardId, data.Security.BoardId, StringComparison.OrdinalIgnoreCase));

        return latestCoupon is not null
            ? ConvertCurrencyAmount(
                latestCoupon.Value,
                latestCoupon.FaceUnit,
                data.Position.CurrencyId,
                referenceData.CurrencyRates,
                quoteDate)
            : ConvertCurrencyAmount(
                data.Security.CouponValue,
                data.Security.FaceUnit,
                data.Position.CurrencyId,
                referenceData.CurrencyRates,
                quoteDate);
    }

    private PositionTradePricing[] CalculatePositionTradePricing(
        PositionData data,
        IReadOnlyCollection<PortfolioTradeRecord> trades,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        PositionMarketPriceData marketPriceData)
    {
        if (data.Security is null)
            return [];

        return trades
            .Where(x => x.SecId == data.Position.SecId
                        && x.BoardId == data.Position.BoardId
                        && string.Equals(
                            x.CurrencyId,
                            data.Position.CurrencyId,
                            StringComparison.OrdinalIgnoreCase))
            .Select(trade =>
            {
                var purchasePricePercent = PortfolioMarketPriceCalculator.CalculateTradePricePercent(
                    trade.Price,
                    trade.FaceValue,
                    data.Security.InitialFaceValue,
                    data.Security.FaceValue,
                    data.Security.FaceUnit,
                    trade.CurrencyId,
                    GetCurrencyRate(rates, data.Security.FaceUnit, trade.TradeDate),
                    GetCurrencyRate(rates, trade.CurrencyId, trade.TradeDate));
                var purchasePriceAtCurrentFaceValue = purchasePricePercent.HasValue
                    ? PortfolioMarketPriceCalculator.Calculate(
                        data.Security.FaceValue,
                        data.Security.FaceUnit,
                        data.Position.CurrencyId,
                        purchasePricePercent.Value,
                        marketPriceData.FaceCurrencyRateToRub,
                        marketPriceData.TradingCurrencyRateToRub)
                    : null;

                return new PositionTradePricing(trade, purchasePricePercent, purchasePriceAtCurrentFaceValue);
            })
            .ToArray();
    }

    private static PortfolioPositionIncome? CalculatePositionIncome(
        PositionData data,
        IReadOnlyCollection<PositionTradePricing> tradePricing,
        PositionMarketPriceData marketPriceData,
        double? couponValue,
        bool isNominalIndexed,
        DateOnly asOf)
    {
        var canNormalizeOpenCost = tradePricing
            .Where(x => x.Trade.Side == TradeSide.Buy)
            .All(x => x.PurchasePricePercent.HasValue && x.PurchasePriceAtCurrentFaceValue.HasValue);
        if (!marketPriceData.MarketPrice.HasValue
            || data.Marketdata?.CurrentPrice is null
            || data.Security is null
            || !canNormalizeOpenCost)
        {
            return null;
        }

        var incomeTrades = tradePricing
            .Select(x => new PositionIncomeTrade(
                x.Trade.TradeDate,
                x.Trade.Side,
                x.Trade.Quantity,
                x.PurchasePriceAtCurrentFaceValue ?? x.Trade.Price,
                x.Trade.Commission,
                x.PurchasePricePercent,
                x.Trade.Price))
            .ToArray();

        return PortfolioPositionCalculator.Calculate(
            incomeTrades,
            asOf,
            marketPriceData.MarketPrice.Value,
            couponValue.GetValueOrDefault(),
            data.Security.CouponPeriod,
            isNominalIndexed);
    }

    private static ValuedPosition CreateValuedPosition(
        PositionData data,
        PositionMarketPriceData marketPriceData,
        double? couponValue,
        PortfolioPositionIncome? income,
        double? averageBuyPriceAtCurrentFaceValue,
        PortfolioPositionValuation valuation,
        bool isNominalIndexed)
    {
        var position = data.Position with
        {
            AverageBuyPrice = income is
                { RemainingQuantity: > 0, ActualRemainingCleanCost: { } actualRemainingCleanCost }
                ? actualRemainingCleanCost / income.RemainingQuantity
                : data.Position.AverageBuyPrice,
            MarketPrice = valuation.MarketPrice,
            MarketValue = valuation.MarketValue,
            UnrealizedPnl = income?.UnrealizedPnl ?? valuation.UnrealizedPnl,
            UnrealizedPnlPercent = income?.UnrealizedPnlPercent ?? valuation.UnrealizedPnlPercent,
            ShortName = data.Security?.ShortName ?? data.Position.SecId,
            AverageDailyVolume = data.VolumeStatistics?.Average,
            MedianDailyVolume = data.VolumeStatistics?.Median,
            ApproximateCouponIncome = couponValue.HasValue ? income?.CouponIncome : null,
            ApproximateTotalPnl = couponValue.HasValue ? income?.TotalPnl : null,
            ApproximateTotalPnlPercent = couponValue.HasValue ? income?.TotalPnlPercent : null,
            AverageBuyPriceAtCurrentFaceValue = averageBuyPriceAtCurrentFaceValue,
            AverageBuyPricePercent = income?.AverageBuyPricePercent,
            CurrentFaceValue = data.Security?.FaceValue,
            FaceUnit = data.Security?.FaceUnit,
            MarketPricePercent = data.Marketdata?.CurrentPrice,
            IsNominalIndexed = isNominalIndexed
        };

        return new ValuedPosition(position, marketPriceData.TradingCurrencyRateToRub);
    }

    private static MoexMarketdata? GetMarketdataValue(
        Dictionary<string, MoexMarketdata> marketdataByKey,
        Dictionary<string, MoexMarketdata[]> marketdataBySecId,
        PortfolioPositionRecord position)
    {
        var marketdataValue = marketdataByKey
            .GetValueOrDefault(InstrumentKey(position.SecId, position.BoardId));

        if (marketdataValue is null && marketdataBySecId.TryGetValue(position.SecId, out var quotes))
        {
            marketdataValue = quotes.FirstOrDefault(x =>
                RelationHelper.BoardCurrency.TryGetValue(x.BoardId, out var currency)
                && string.Equals(currency, position.CurrencyId, StringComparison.OrdinalIgnoreCase));
        }

        return marketdataValue;
    }

    private static string InstrumentKey(string secId, string boardId) => $"{secId}\u001F{boardId}";

    private static double? ConvertCurrencyAmount(
        double amount,
        string sourceCurrency,
        string targetCurrency,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        DateOnly date)
    {
        var sourceRate = GetCurrencyRate(rates, sourceCurrency, date);
        var targetRate = GetCurrencyRate(rates, targetCurrency, date);
        return sourceRate.HasValue && targetRate.HasValue ? amount * sourceRate.Value / targetRate.Value : null;
    }

    private async Task<IReadOnlyList<CurrencyRateSnapshot>> LoadCurrencyRatesAsync(
        IReadOnlyCollection<string> currencies,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CurrencyRates.AsNoTracking()
            .Where(x => currencies.Contains(x.CurrencyCode))
            .Select(x => new CurrencyRateSnapshot(x.RateDate, x.CurrencyCode, x.UnitRate))
            .ToListAsync(cancellationToken);
    }

    private static double? GetCurrencyRate(
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        string currency,
        DateOnly date)
    {
        if (IsRubleCurrency(currency))
            return 1;
        
        return rates
            .Where(x => string.Equals(x.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase) &&
                        x.RateDate <= date)
            .OrderByDescending(x => x.RateDate)
            .Select(x => (double?)x.UnitRate)
            .FirstOrDefault();
    }

    private static bool IsRubleCurrency(string currency) =>
        string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase)
        || string.Equals(currency, "SUR", StringComparison.OrdinalIgnoreCase);

    private sealed record PositionSourceData(
        IReadOnlyList<PortfolioPositionRecord> Positions,
        IReadOnlyList<PortfolioTradeRecord> Trades,
        IReadOnlyList<MoexMarketdata> Marketdata,
        IReadOnlyList<MoexBond> Securities,
        IReadOnlyDictionary<string, DailyVolumeStatistics> VolumeStatistics)
    {
        public static PositionSourceData Empty => 
            new([], [], [], [], new Dictionary<string, DailyVolumeStatistics>(StringComparer.OrdinalIgnoreCase));
    };

    private sealed record PositionData(
        PortfolioPositionRecord Position,
        MoexMarketdata? Marketdata,
        MoexBond? Security,
        DailyVolumeStatistics? VolumeStatistics)
    {
        public bool IsNominalIndexed => Security is not null
                                        && Security.InitialFaceValue > 0
                                        && Security.FaceValue > Security.InitialFaceValue;
    }

    private sealed record PositionAdditionalData(
        IReadOnlyList<PublishedCouponValue> CouponHistory,
        IReadOnlyList<CurrencyRateSnapshot> CurrencyRates);

    private sealed record CurrencyRateSnapshot(DateOnly RateDate, string CurrencyCode, double UnitRate);

    private sealed record PositionMarketPriceData(
        DateOnly QuoteDate,
        double? TradingCurrencyRateToRub,
        double? FaceCurrencyRateToRub,
        double? MarketPrice);

    private sealed record ValuedPosition(PortfolioPositionRecord Position, double? TradingCurrencyRateToRub);

    private sealed record PositionTradePricing(
        PortfolioTradeRecord Trade,
        double? PurchasePricePercent,
        double? PurchasePriceAtCurrentFaceValue);
}

public sealed record PortfolioOverview(
    IReadOnlyList<CashBalance> Balances,
    PortfolioSummaryRecord Summary);
