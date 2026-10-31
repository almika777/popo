using Microsoft.EntityFrameworkCore;
using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.Extensions;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;
using Popo.Storage;

namespace Popo.Api.Services.Position;

public interface IPortfolioPositionsService
{
    Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken);
    Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken);

    /// <summary>Возвращает позиции фондов с доступной рыночной оценкой MOEX и рассчитанным результатом.</summary>
    /// <param name="cancellationToken">Токен отмены загрузки данных.</param>
    /// <returns>Позиции фондов; при недоступной котировке сохранённые данные позиции остаются в ответе.</returns>
    Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken);

    /// <summary>Возвращает исходные цены, количества и валюты позиций для аналитики портфеля.</summary>
    /// <param name="cancellationToken">Токен отмены загрузки данных.</param>
    /// <returns>Позиции с ценой в валюте торгов и отдельной валютой номинала.</returns>
    Task<IReadOnlyList<PortfolioCurrencyAnalyticsAsset>> GetCurrencyAnalyticsAssetsAsync(
        CancellationToken cancellationToken);
}

public sealed class PortfolioPositionsService(
    IPortfolioMoneyMarketFundsProvider moneyMarketFundsProvider,
    IPortfolioTradesProvider tradesProvider,
    IPortfolioPositionsProvider positionsProvider,
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    IHistoryProvider historyProvider,
    IPortfolioCouponProvider couponProvider) : IPortfolioPositionsService
{
    private static readonly PositionCalculatorFactory PositionCalculatorFactory = new();

    public async Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken)
    {
        var valuedPositions = await LoadPositionsAsync(cancellationToken);
        var positions = valuedPositions.Select(x => x.Position).ToArray();
        var marketValueRub = valuedPositions.Sum(x =>
            x.Position.MarketValue.GetValueOrDefault() * x.TradingCurrencyRateToRub.GetValueOrDefault());
        var unrealizedPnlRub = valuedPositions.All(x => x.Position.UnrealizedPnl.HasValue)
            ? valuedPositions.Sum(x => x.Position.UnrealizedPnl!.Value
                                       * x.TradingCurrencyRateToRub.GetValueOrDefault())
            : (double?)null;
        var investedAmountRub = unrealizedPnlRub.HasValue
            ? marketValueRub - unrealizedPnlRub.Value
            : (double?)null;
        var totals = new PositionTotalsResponse(
            marketValueRub,
            investedAmountRub,
            unrealizedPnlRub,
            !investedAmountRub.HasValue || investedAmountRub.Value == 0
                ? null
                : unrealizedPnlRub!.Value / investedAmountRub.Value * 100);

        return new PositionsPageResponse(positions, totals);
    }

    public async Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var valuedPositions = await LoadPositionsAsync(cancellationToken);
        return valuedPositions.Select(x => x.Position).ToArray();
    }

    public async Task<IReadOnlyList<PortfolioCurrencyAnalyticsAsset>> GetCurrencyAnalyticsAssetsAsync(
        CancellationToken cancellationToken)
    {
        var asOf = MoscowTime.Today;
        var positions = await positionsProvider.GetPositionsAsync(asOf, cancellationToken);
        if (positions.Count == 0)
        {
            return [];
        }

        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        await Task.WhenAll(marketdataTask, securitiesTask);
        var positionData = ResolvePositionData(new PositionSourceData(
            positions,
            [],
            (await marketdataTask).SelectMany(item => item.Value).ToArray(),
            (await securitiesTask).SelectMany(item => item.Value).ToArray(),
            new Dictionary<string, DailyVolumeStatistics>(StringComparer.OrdinalIgnoreCase)));
        var currencies = positionData
            .SelectMany(item => HasCurrentMarketPrice(item) && item.Security is not null
                ? new[] { item.Position.CurrencyId, item.Security.FaceUnit }
                : [item.Position.CurrencyId])
            .Where(currency => !IsRubleCurrency(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = currencies.Length == 0 ? [] : await LoadCurrencyRatesAsync(currencies, cancellationToken);

        return positionData.Select(item =>
        {
            var pricing = CalculateMarketPrice(item, rates, asOf);
            var name = !string.IsNullOrWhiteSpace(item.Security?.ShortName)
                ? item.Security.ShortName
                : !string.IsNullOrWhiteSpace(item.Position.ShortName)
                    ? item.Position.ShortName
                    : item.Position.SecId;

            return new PortfolioCurrencyAnalyticsAsset(
                $"position:{item.Position.SecId}:{item.Position.BoardId}:{item.Position.CurrencyId}",
                PortfolioCurrencyAnalyticsAssetKind.Position,
                name,
                item.Position.CurrencyId,
                item.Security?.FaceUnit,
                item.Position.Quantity,
                pricing.MarketPrice,
                pricing.TradingCurrencyRateToRub);
        }).ToArray();
    }

    public async Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var positionsTask = LoadPositionMarketValuesAsync(asOf, cancellationToken);
        var fundsTask = GetMoneyMarketFundsAsync(cancellationToken);
        await Task.WhenAll(positionsTask, fundsTask);
        var positionValues = await positionsTask;
        var positionsValueRub = positionValues.Sum(value => value.GetValueOrDefault());
        var funds = await fundsTask;
        var fundsValueRub = funds.Sum(x => x.CurrentValue.GetValueOrDefault());

        var summary = new PortfolioSummaryRecord(
            asOf,
            positionsValueRub + fundsValueRub,
            positionsValueRub,
            fundsValueRub);
        return new PortfolioOverview(summary);
    }

    /// <summary>Оценивает текущие позиции по котировкам и курсам без загрузки сделок, купонов и статистики оборотов для расчёта P&amp;L.</summary>
    private async Task<IReadOnlyList<double?>> LoadPositionMarketValuesAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var positions = await positionsProvider.GetPositionsAsync(asOf, cancellationToken);
        if (positions.Count == 0)
            return [];

        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        await Task.WhenAll(marketdataTask, securitiesTask);
        var data = ResolvePositionData(new PositionSourceData(
            positions,
            [],
            (await marketdataTask).SelectMany(item => item.Value).ToArray(),
            (await securitiesTask).SelectMany(item => item.Value).ToArray(),
            new Dictionary<string, DailyVolumeStatistics>(StringComparer.OrdinalIgnoreCase)));
        var currencies = data
            .SelectMany(item => new[] { item.Position.CurrencyId, item.Security?.FaceUnit ?? item.Position.CurrencyId })
            .Where(currency => !IsRubleCurrency(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = currencies.Length == 0 ? [] : await LoadCurrencyRatesAsync(currencies, cancellationToken);
        return data.Select(item =>
        {
            var pricing = CalculateMarketPrice(item, rates, asOf);
            return pricing.MarketPrice * item.Position.Quantity * pricing.TradingCurrencyRateToRub;
        }).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(
        CancellationToken cancellationToken)
    {
        var funds = await moneyMarketFundsProvider.GetMoneyMarketFundsAsync(cancellationToken);
        var fundsMarketdata = await Task.WhenAll(funds.Select(x => moexHttpClient
            .GetMoneyMarketFundMarketdataAsync(x.SecId, x.BoardId, cancellationToken)));
        return funds.Select(fund =>
        {
            var quote = fundsMarketdata.First(x => x.SecId == fund.SecId && x.BoardId == fund.BoardId);
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
        }).ToArray();
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
            var security = securitiesByKey.GetValueOrDefault(securityKey);
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
        var couponHistory = await couponProvider.GetCouponScheduleAsync(bondKeys, cancellationToken);
        var rateCurrencies = selections
            .Where(x => x.Security is not null)
            .SelectMany(x => new[] { x.Security!.FaceUnit, x.Position.CurrencyId })
            .Concat(couponHistory.Select(x => x.FaceUnit))
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
        var calculator = PositionCalculatorFactory.Create(
            data.Security?.FaceUnit,
            data.Position.CurrencyId,
            data.Security?.BondType,
            data.Security?.BondSubType);
        var tradePricing = CalculatePositionTradePricing(
            data,
            sourceData.Trades,
            referenceData.CurrencyRates,
            marketPricing,
            calculator is not ZoPositionCalculator);
        var isNominalIndexed = data.IsNominalIndexed
                               || data.Security is { InitialFaceValue: <= 0, FaceValue: > 0 } security
                               && HasOpenLotNominalGrowth(tradePricing, security.FaceValue);
        var canNormalizeOpenCost = tradePricing
            .Where(x => x.Trade.Side == TradeSide.Buy)
            .All(x => x.PurchasePricePercent.HasValue && x.PurchasePriceAtCurrentFaceValue.HasValue);
        var positionIncomeTrades = tradePricing.Select(x => new PositionIncomeTrade(
            x.Trade.TradeDate,
            x.Trade.Side,
            x.Trade.Quantity,
            x.PurchasePriceAtCurrentFaceValue ?? x.Trade.Price,
            x.Trade.Commission,
            x.PurchasePricePercent,
            x.Trade.Price,
            x.Trade.AccruedInterest)).ToArray();
        var positionCalculation = calculator is DefaultPositionCalculator
                                  && (!marketPricing.MarketPrice.HasValue
                                      || data.Marketdata?.CurrentPrice is null
                                      || data.Security is null
                                      || !canNormalizeOpenCost)
            ? new PositionCalculationResult(
                null,
                PortfolioPositionCalculator.Calculate(data.Position, marketPricing.MarketPrice),
                null,
                data.Position.AverageBuyPrice)
            : calculator.Calculate(new PositionCalculationInput(
                data.Position,
                positionIncomeTrades,
                asOf,
                marketPricing.MarketPrice,
                couponValue.GetValueOrDefault(),
                data.Security?.CouponPeriod ?? 0,
                isNominalIndexed,
                data.Security?.Accruedint,
                GetPositionCouponPayments(data, referenceData),
                data.Security?.CouponFrequency,
                data.Security?.NextCouponDate,
                marketPricing.QuoteDate));
        var income = positionCalculation.Income;
        var averageBuyPriceAtCurrentFaceValue = positionCalculation.AverageBuyPriceAtCurrentFaceValue;
        var valuation = positionCalculation.Valuation;
        var annualizedReturnUnavailableReason = income is { IsCouponIncomeComplete: true }
                                               && couponValue.HasValue
                                               && income.AnnualizedTotalPnlPercent is null
                                               && PositionOpenLotCalculator.Calculate(positionIncomeTrades, asOf)
                                                   .Lots.Any(lot => lot.TradeDate > marketPricing.QuoteDate)
            ? "QuoteBeforeLatestOpenPurchase"
            : null;

        return CreateValuedPosition(
            data,
            marketPricing,
            couponValue,
            income,
            averageBuyPriceAtCurrentFaceValue,
            valuation,
            isNominalIndexed,
            positionCalculation.AverageBuyPrice,
            calculator is FloaterPositionCalculator,
            annualizedReturnUnavailableReason);
    }

    private static bool HasOpenLotNominalGrowth(
        IReadOnlyCollection<PositionTradePricing> tradePricing,
        double currentFaceValue)
    {
        var openLots = new List<(double Quantity, double FaceValue)>();
        foreach (var day in tradePricing.GroupBy(x => x.Trade.TradeDate).OrderBy(x => x.Key))
        {
            foreach (var buy in day.Where(x => x.Trade.Side == TradeSide.Buy))
            {
                var tradeFaceValue = buy.Trade.FaceValue;
                if (!double.IsFinite(tradeFaceValue) || tradeFaceValue <= 0)
                    return false;

                openLots.Add((buy.Trade.Quantity, tradeFaceValue));
            }

            var soldQuantity = day.Where(x => x.Trade.Side == TradeSide.Sell).Sum(x => x.Trade.Quantity);
            var availableQuantity = openLots.Sum(x => x.Quantity);
            if (soldQuantity > availableQuantity)
                return false;
            if (soldQuantity <= 0)
                continue;

            var retainedFraction = (availableQuantity - soldQuantity) / availableQuantity;
            if (retainedFraction <= 0)
            {
                openLots.Clear();
                continue;
            }

            for (var index = 0; index < openLots.Count; index++)
                openLots[index] = (openLots[index].Quantity * retainedFraction, openLots[index].FaceValue);
        }

        return openLots.Any(x => x.Quantity > 0 && x.FaceValue < currentFaceValue);
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

    private static bool HasCurrentMarketPrice(PositionData data) =>
        data.Marketdata?.CurrentPrice is { } price && double.IsFinite(price) && price > 0;

    private double? GetPositionCouponValue(
        PositionData data,
        PositionAdditionalData referenceData,
        DateOnly quoteDate)
    {
        if (data.Security is null)
            return null;

        var latestCoupon = referenceData.CouponHistory
            .Where(x => x.Date <= quoteDate && x.Value.HasValue)
            .OrderByDescending(x => x.Date)
            .FirstOrDefault(x =>
                string.Equals(x.SecId, data.Security.SecId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Isin, data.Security.Isin, StringComparison.OrdinalIgnoreCase));

        return latestCoupon is not null
            ? ConvertCurrencyAmount(
                latestCoupon.Value!.Value,
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

    private static IReadOnlyCollection<PositionCouponPayment> GetPositionCouponPayments(
        PositionData data,
        PositionAdditionalData referenceData)
    {
        if (data.Security is null)
            return [];

        return referenceData.CouponHistory
            .Where(x => string.Equals(x.SecId, data.Security.SecId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.Isin, data.Security.Isin, StringComparison.OrdinalIgnoreCase))
            .Select(x => new PositionCouponPayment(
                x.Date,
                x.PeriodStartDate,
                x.Value,
                x.FaceUnit,
                GetCurrencyRate(referenceData.CurrencyRates, x.FaceUnit, x.Date),
                x.RecordDate,
                x.ValueInRub))
            .ToArray();
    }

    private PositionTradePricing[] CalculatePositionTradePricing(
        PositionData data,
        IReadOnlyCollection<PortfolioTradeRecord> trades,
        IReadOnlyCollection<CurrencyRateSnapshot> rates,
        PositionMarketPriceData marketPriceData,
        bool normalizeCleanPrices)
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
                var purchasePriceAtCurrentFaceValue = normalizeCleanPrices && purchasePricePercent.HasValue
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

    private static ValuedPosition CreateValuedPosition(
        PositionData data,
        PositionMarketPriceData marketPriceData,
        double? couponValue,
        PortfolioPositionIncome? income,
        double? averageBuyPriceAtCurrentFaceValue,
        PortfolioPositionValuation valuation,
        bool isNominalIndexed,
        double? averageBuyPrice,
        bool isFloaterCalculator,
        string? annualizedReturnUnavailableReason)
    {
        var completeCouponIncome = income is { IsCouponIncomeComplete: true }
                                   && (isFloaterCalculator || couponValue.HasValue)
            ? income
            : null;
        var position = data.Position with
        {
            AverageBuyPrice = averageBuyPrice ?? (income is
                { RemainingQuantity: > 0, ActualRemainingCleanCost: { } actualRemainingCleanCost }
                ? actualRemainingCleanCost / income.RemainingQuantity
                : data.Position.AverageBuyPrice),
            MarketPrice = valuation.MarketPrice,
            MarketValue = valuation.MarketValue,
            UnrealizedPnl = income?.UnrealizedPnl ?? valuation.UnrealizedPnl,
            UnrealizedPnlPercent = income?.UnrealizedPnlPercent ?? valuation.UnrealizedPnlPercent,
            ShortName = data.Security?.ShortName ?? data.Position.SecId,
            AverageDailyVolume = data.VolumeStatistics?.Average,
            MedianDailyVolume = data.VolumeStatistics?.Median,
            ApproximateCouponIncome = completeCouponIncome?.CouponIncome,
            ApproximateTotalPnl = completeCouponIncome?.TotalPnl,
            ApproximateTotalPnlPercent = completeCouponIncome?.TotalPnlPercent,
            ApproximateAnnualizedTotalPnlPercent = (completeCouponIncome?.AnnualizedTotalPnlPercent).Round(),
            ApproximateAnnualizedReturnBreakdown = completeCouponIncome?.AnnualizedTotalPnlPercent is not null
                ? RoundAnnualizedBreakdown(completeCouponIncome.AnnualizedReturnBreakdown)
                : null,
            ApproximateAnnualizedReturnUnavailableReason = annualizedReturnUnavailableReason,
            AverageBuyPriceAtCurrentFaceValue = averageBuyPriceAtCurrentFaceValue,
            AverageBuyPricePercent = income?.AverageBuyPricePercent,
            CurrentFaceValue = data.Security?.FaceValue,
            FaceUnit = data.Security?.FaceUnit,
            MarketPricePercent = data.Marketdata?.CurrentPrice,
            IsNominalIndexed = isNominalIndexed
        };

        return new ValuedPosition(position, marketPriceData.TradingCurrencyRateToRub);
    }

    private static PositionAnnualizedReturnBreakdown? RoundAnnualizedBreakdown(
        PositionAnnualizedReturnBreakdown? breakdown) => breakdown is null ? null : breakdown with
    {
        TotalPnl = breakdown.TotalPnl.Round(),
        InvestedCapitalDays = breakdown.InvestedCapitalDays.Round(),
        AnnualizedPercent = breakdown.AnnualizedPercent.Round(),
        Lots = breakdown.Lots.Select(lot => lot with
        {
            Quantity = lot.Quantity.Round(),
            PurchaseCleanPricePerUnit = lot.PurchaseCleanPricePerUnit.Round(),
            PaidAccruedInterestPerUnit = lot.PaidAccruedInterestPerUnit.Round(),
            CommissionPerUnit = lot.CommissionPerUnit.Round(),
            InvestedCapital = lot.InvestedCapital.Round(),
            CapitalDays = lot.CapitalDays.Round()
        }).ToArray()
    };

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

public sealed record PortfolioOverview(PortfolioSummaryRecord Summary);
