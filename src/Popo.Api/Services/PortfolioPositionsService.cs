using Microsoft.EntityFrameworkCore;
using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Recommendations;
using Popo.Storage;

namespace Popo.Api.Services;

public interface IPortfolioPositionsService
{
    Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken);
    Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken);
    Task<PortfolioSummaryRecord> GetSummaryAsync(DateOnly asOf, CancellationToken cancellationToken);
    Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken);
}

public sealed class PortfolioPositionsService(
    IPortfolioLedgerProvider ledgerProvider,
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory,
    IHistoryProvider historyProvider,
    IPortfolioCouponProvider couponProvider,
    IPositionRecommendationStore recommendationStore) : IPortfolioPositionsService
{
    public async Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken)
    {
        var recommendationTask = recommendationStore.GetAsync(cancellationToken);
        var valuedPositionsTask = LoadValuedPositionsAsync(cancellationToken);
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
        var valuedPositions = await LoadValuedPositionsAsync(cancellationToken);
        return valuedPositions.Select(x => x.Position).ToArray();
    }

    public async Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken)
    {
        var valuedPositions = await LoadValuedPositionsAsync(cancellationToken);
        var cashBalances = await ledgerProvider.GetCashBalancesAsync(asOf, cancellationToken);
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

    public async Task<PortfolioSummaryRecord> GetSummaryAsync(DateOnly asOf, CancellationToken cancellationToken) =>
        (await GetOverviewAsync(asOf, cancellationToken)).Summary;

    public async Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken)
    {
        var funds = await ledgerProvider.GetMoneyMarketFundsAsync(cancellationToken);
        return await Task.WhenAll(funds.Select(async fund =>
        {
            try
            {
                var quote = await moexHttpClient.GetMoneyMarketFundMarketdataAsync(fund.SecId, fund.BoardId, cancellationToken);
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

    private async Task<IReadOnlyList<ValuedPosition>> LoadValuedPositionsAsync(CancellationToken cancellationToken)
    {
        var positions = await ledgerProvider.GetPositionsAsync(cancellationToken);
        if (positions.Count == 0)
            return [];
        
        var tradesTask = ledgerProvider.GetTradesAsync(cancellationToken);
        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        var volumeStatisticsTask = historyProvider.GetDailyVolumeStatistics(cancellationToken);
        await Task.WhenAll(tradesTask, marketdataTask, securitiesTask, volumeStatisticsTask);

        var trades = await tradesTask;
        var marketdata = (await marketdataTask).SelectMany(x => x.Value).ToArray();
        var securities = (await securitiesTask).SelectMany(x => x.Value).ToArray();
        var volumeStatistics = await volumeStatisticsTask;
        var marketdataByKey = marketdata
            .GroupBy(x => InstrumentKey(x.SecId, x.BoardId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var marketdataBySecId = marketdata
            .GroupBy(x => x.SecId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var securitiesByKey = securities
            .GroupBy(x => InstrumentKey(x.SecId, x.BoardId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var selections = positions.Select(position =>
        {
            var quote = marketdataByKey.GetValueOrDefault(InstrumentKey(position.SecId, position.BoardId));
            if (quote is null && marketdataBySecId.TryGetValue(position.SecId, out var quotes))
            {
                quote = quotes.FirstOrDefault(x => RelationHelper.BoardCurrency.TryGetValue(x.BoardId, out var currency)
                    && string.Equals(currency, position.CurrencyId, StringComparison.OrdinalIgnoreCase));
            }

            var security = quote is null
                ? null
                : securitiesByKey.GetValueOrDefault(InstrumentKey(quote.SecId, quote.BoardId))
                    ?? securitiesByKey.GetValueOrDefault(InstrumentKey(position.SecId, position.BoardId));
            return new { position, quote, security };
        }).ToArray();
        var bondKeys = selections
            .Where(x => x.security is not null)
            .Select(x => new PortfolioBondKey(x.security!.SecId, x.security.Isin, x.security.BoardId))
            .Distinct()
            .ToArray();
        var couponHistory = await couponProvider.GetLatestPublishedAsync(
            bondKeys,
            MoscowTime.Today,
            cancellationToken);
        var rateCurrencies = selections
            .Where(x => x.security is not null)
            .SelectMany(x => new[] { x.security!.FaceUnit, x.position.CurrencyId })
            .Where(x => !IsRubleCurrency(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = rateCurrencies.Length == 0
            ? []
            : await LoadCurrencyRatesAsync(rateCurrencies, cancellationToken);

        var valuedPositions = selections.Select(selection =>
        {
            var position = selection.position;
            var quote = selection.quote;
            var security = selection.security;
            var quoteDate = quote?.SysTime is { } sysTime ? DateOnly.FromDateTime(sysTime) : MoscowTime.Today;
            var tradingCurrencyRateToRub = GetCurrencyRate(rates, position.CurrencyId, quoteDate);
            var faceCurrencyRateToRub = security is null
                ? null
                : GetCurrencyRate(rates, security.FaceUnit, quoteDate);
            var marketPrice = security is null || quote is null
                ? null
                : PortfolioMarketPriceCalculator.Calculate(
                    security.FaceValue,
                    security.FaceUnit,
                    position.CurrencyId,
                    quote.CurrentPrice,
                    faceCurrencyRateToRub,
                    tradingCurrencyRateToRub);
            var latestCoupon = security is null
                ? null
                : couponHistory.FirstOrDefault(x =>
                    string.Equals(x.SecId, security.SecId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Isin, security.Isin, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.BoardId, security.BoardId, StringComparison.OrdinalIgnoreCase));
            var couponValue = latestCoupon is not null
                ? ConvertCurrencyAmount(latestCoupon.Value, latestCoupon.FaceUnit, position.CurrencyId, rates, quoteDate)
                : security is null
                    ? null
                    : ConvertCurrencyAmount(security.CouponValue, security.FaceUnit, position.CurrencyId, rates, quoteDate);
            PositionTradePricing[] positionTrades = security is null
                ? []
                : trades
                    .Where(x => x.SecId == position.SecId
                        && x.BoardId == position.BoardId
                        && string.Equals(x.CurrencyId, position.CurrencyId, StringComparison.OrdinalIgnoreCase))
                    .Select(trade =>
                    {
                        var purchasePricePercent = PortfolioMarketPriceCalculator.CalculatePricePercent(
                            trade.Price,
                            trade.FaceValue,
                            security.FaceUnit,
                            trade.CurrencyId,
                            GetCurrencyRate(rates, security.FaceUnit, trade.TradeDate),
                            GetCurrencyRate(rates, trade.CurrencyId, trade.TradeDate));
                        var purchasePriceAtCurrentFaceValue = purchasePricePercent.HasValue
                            ? PortfolioMarketPriceCalculator.Calculate(
                                security.FaceValue,
                                security.FaceUnit,
                                position.CurrencyId,
                                purchasePricePercent.Value,
                                faceCurrencyRateToRub,
                                tradingCurrencyRateToRub)
                            : null;

                        return new PositionTradePricing(trade, purchasePricePercent, purchasePriceAtCurrentFaceValue);
                    })
                    .ToArray();
            var canNormalizeOpenCost = positionTrades
                .Where(x => x.Trade.Side == TradeSide.Buy)
                .All(x => x.PurchasePricePercent.HasValue && x.PurchasePriceAtCurrentFaceValue.HasValue);
            var income = marketPrice.HasValue
                         && quote?.CurrentPrice is { } marketPricePercent
                         && security is not null
                         && canNormalizeOpenCost
                ? PortfolioPositionIncomeCalculator.Calculate(
                    positionTrades
                        .Select(x => new PositionIncomeTrade(
                            x.Trade.TradeDate,
                            x.Trade.Side,
                            x.Trade.Quantity,
                            x.PurchasePriceAtCurrentFaceValue ?? x.Trade.Price,
                            x.Trade.Commission,
                            x.PurchasePricePercent))
                        .ToArray(),
                    MoscowTime.Today,
                    marketPrice.Value,
                    couponValue.GetValueOrDefault(),
                    security.CouponPeriod)
                : null;
            double? averageBuyPriceAtCurrentFaceValue = income is { RemainingQuantity: > 0 }
                ? income.RemainingCleanCost / income.RemainingQuantity
                : null;
            var valuation = PortfolioPositionValuationCalculator.Calculate(
                position,
                marketPrice,
                averageBuyPriceAtCurrentFaceValue);

            return new ValuedPosition(
                position with
                {
                    MarketPrice = valuation.MarketPrice,
                    MarketValue = valuation.MarketValue,
                    UnrealizedPnl = valuation.UnrealizedPnl,
                    UnrealizedPnlPercent = valuation.UnrealizedPnlPercent,
                    ShortName = security?.ShortName ?? position.SecId,
                    AverageDailyVolume = volumeStatistics.GetValueOrDefault(position.SecId)?.Average,
                    MedianDailyVolume = volumeStatistics.GetValueOrDefault(position.SecId)?.Median,
                    ApproximateCouponIncome = couponValue.HasValue ? income?.CouponIncome : null,
                    ApproximateTotalPnl = couponValue.HasValue ? income?.TotalPnl : null,
                    ApproximateTotalPnlPercent = couponValue.HasValue ? income?.TotalPnlPercent : null,
                    AverageBuyPriceAtCurrentFaceValue = averageBuyPriceAtCurrentFaceValue,
                    AverageBuyPricePercent = income?.AverageBuyPricePercent,
                    CurrentFaceValue = security?.FaceValue,
                    FaceUnit = security?.FaceUnit,
                    MarketPricePercent = quote?.CurrentPrice
                },
                tradingCurrencyRateToRub);
        }).ToArray();
        return valuedPositions;
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
        {
            return 1;
        }

        return rates
            .Where(x => string.Equals(x.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase) && x.RateDate <= date)
            .OrderByDescending(x => x.RateDate)
            .Select(x => (double?)x.UnitRate)
            .FirstOrDefault();
    }

    private static bool IsRubleCurrency(string currency) =>
        string.Equals(currency, "RUB", StringComparison.OrdinalIgnoreCase)
        || string.Equals(currency, "SUR", StringComparison.OrdinalIgnoreCase);

    private sealed record CurrencyRateSnapshot(DateOnly RateDate, string CurrencyCode, double UnitRate);
    private sealed record ValuedPosition(PortfolioPositionRecord Position, double? TradingCurrencyRateToRub);
    private sealed record PositionTradePricing(
        PortfolioTradeRecord Trade,
        double? PurchasePricePercent,
        double? PurchasePriceAtCurrentFaceValue);
}

public sealed record PortfolioOverview(
    IReadOnlyList<CashBalance> Balances,
    PortfolioSummaryRecord Summary);
