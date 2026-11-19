using Microsoft.EntityFrameworkCore;
using Popo.Api.Models;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;
using Popo.Storage;
using static Popo.Api.Services.Position.PositionCurrencyRates;

namespace Popo.Api.Services.Position;

public interface IPortfolioPositionsService
{
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
    IPortfolioHoldingsProvider holdingsProvider,
    IMoexHttpClient moexHttpClient,
    IDbContextFactory<PopoDbContext> dbContextFactory) : IPortfolioPositionsService
{
    public async Task<IReadOnlyList<PortfolioCurrencyAnalyticsAsset>> GetCurrencyAnalyticsAssetsAsync(
        CancellationToken cancellationToken)
    {
        var asOf = MoscowTime.Today;
        var positions = await holdingsProvider.GetHoldingsAsync(asOf, cancellationToken);
        if (positions.Count == 0)
        {
            return [];
        }

        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        await Task.WhenAll(marketdataTask, securitiesTask);
        var positionData = PositionDataResolver.ResolvePositionData(new PositionSourceData(
            positions,
            (await marketdataTask).SelectMany(item => item.Value).ToArray(),
            (await securitiesTask).SelectMany(item => item.Value).ToArray()));
        var currencies = positionData
            .SelectMany(item => PositionValuationCalculator.HasCurrentMarketPrice(item) && item.Security is not null
                ? new[] { item.Position.CurrencyId, item.Security.FaceUnit }
                : [item.Position.CurrencyId])
            .Where(currency => !IsRubleCurrency(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = currencies.Length == 0 ? [] : await LoadCurrencyRatesAsync(currencies, cancellationToken);

        return positionData.Select(item =>
        {
            var pricing = PositionValuationCalculator.CalculateMarketPrice(item, rates, asOf);
            var name = !string.IsNullOrWhiteSpace(item.Security?.ShortName)
                ? item.Security.ShortName
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
        var positions = await holdingsProvider.GetHoldingsAsync(asOf, cancellationToken);
        if (positions.Count == 0)
            return [];

        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        await Task.WhenAll(marketdataTask, securitiesTask);
        var data = PositionDataResolver.ResolvePositionData(new PositionSourceData(
            positions,
            (await marketdataTask).SelectMany(item => item.Value).ToArray(),
            (await securitiesTask).SelectMany(item => item.Value).ToArray()));
        var currencies = data
            .SelectMany(item => new[] { item.Position.CurrencyId, item.Security?.FaceUnit ?? item.Position.CurrencyId })
            .Where(currency => !IsRubleCurrency(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rates = currencies.Length == 0 ? [] : await LoadCurrencyRatesAsync(currencies, cancellationToken);
        return data.Select(item =>
        {
            var pricing = PositionValuationCalculator.CalculateMarketPrice(item, rates, asOf);
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

}

public sealed record PortfolioOverview(PortfolioSummaryRecord Summary);
