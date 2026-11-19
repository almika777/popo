using Popo.Api.Models;
using Popo.Api.Services.Position.Builders;
using Popo.Api.Validators;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;

namespace Popo.Api.Services.Position;

public class OpenPositionService(
    IPortfolioTradesProvider portfolioTradesProvider,
    IMoexHttpClient moexHttpClient,
    IPositionBuilderFactory positionBuilderFactory,
    OpenPositionReferenceDataLoader referenceDataLoader) : IOpenPositionsService
{
    public async Task<PositionsPageResponse> GetAsync(CancellationToken cancellationToken)
    {
        var tradesTask = portfolioTradesProvider.GetTradesAsync(cancellationToken);
        var securitiesTask = moexHttpClient.GetActiveBondsSecuritiesAsync(cancellationToken);
        var marketdataTask = moexHttpClient.GetActiveBondsMarketdataAsync(cancellationToken);
        await Task.WhenAll(tradesTask, securitiesTask, marketdataTask);

        var trades = await tradesTask;
        var securities = (await securitiesTask)
            .SelectMany(x => x.Value)
            .ToDictionary(x => new SecBoard(x.SecId, x.BoardId));

        var marketdatas = (await marketdataTask)
            .SelectMany(x => x.Value)
            .ToDictionary(x => new SecBoard(x.SecId, x.BoardId));

        var asOf = MoscowTime.Today;
        var openGroups = trades.GroupBy(trade => new SecBoard(trade.SecId, trade.BoardId))
            .Where(group => group.Sum(trade => trade.Side == TradeSide.Buy ? trade.Quantity : -trade.Quantity) != 0)
            .Where(group => securities.ContainsKey(group.Key))
            .ToArray();
        var referenceData = await referenceDataLoader.LoadAsync(openGroups, securities, asOf, cancellationToken);
        var positions = openGroups
            .Select(group => BuildPosition(group, securities[group.Key], marketdatas[group.Key], referenceData, asOf))
            .ToArray();
        
        return new PositionsPageResponse(positions,
            OpenPositionTotalsCalculator.Calculate(positions, referenceData.CurrentRates));
    }

    private PositionResponse BuildPosition(
        IGrouping<SecBoard, PortfolioTradeRecord> trades,
        MoexBond security,
        MoexMarketdata marketdata,
        OpenPositionReferenceData referenceData,
        DateOnly asOf)
    {
        var builder = positionBuilderFactory.Get(security.BondType);
        var input = new OpenPositionBuildInput
        {
            Trades = trades.ToList(),
            Marketdata = marketdata,
            Security = security,
            AsOf = asOf,
            Coupons = referenceData.CouponsBySecId[trades.Key.SecId].ToArray(),
            CurrencyRates = referenceData.AllRates
        };

        if (builder is CurrencyOpenPositionBuilder)
        {
            var validation = new CurrencyOpenPositionInputValidator().Validate(input);
            if (!validation.IsValid)
                throw new InvalidApplicationRequestException($"Не удалось рассчитать {security.SecId}: {validation}");
        }

        return builder.Build(input) with { BondType = security.BondType };
    }
}