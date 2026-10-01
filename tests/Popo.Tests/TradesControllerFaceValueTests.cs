using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Popo.Api.Controllers;
using Popo.Api.Models;
using Popo.Api.Services.Trades;
using Popo.Core.Bonds;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;

namespace Popo.Tests;

public sealed class TradesControllerFaceValueTests
{
    [Test]
    public void TradesController_DependsOnlyOnApplicationService()
    {
        var constructorParameters = typeof(TradesController).GetConstructors().Single().GetParameters();

        Assert.That(constructorParameters.Select(parameter => parameter.ParameterType),
            Is.EqualTo(new[] { typeof(PortfolioTradesService) }));
    }

    [Test]
    public async Task AddTrade_UsesFaceValueAtTradeDateInsteadOfCurrentBondNominal()
    {
        var tradeProvider = new CapturingTradesProvider();
        var bondsService = new HistoricalBondsService(500);
        var controller = new TradesController(
            new PortfolioTradesService(tradeProvider, bondsService, new EmptyMoexHttpClient()));
        var request = new UpsertPortfolioTradeRequest(
            "RU000A", "TQCB", "RUB", new DateOnly(2024, 6, 20), TradeSide.Buy,
            1, 500, 1_000, 0, 0);

        await controller.AddTrade(request, CancellationToken.None);

        Assert.That(tradeProvider.AddedTrade?.FaceValue, Is.EqualTo(500));
        Assert.That(bondsService.Requests.Single().TradeDate, Is.EqualTo(new DateOnly(2024, 6, 20)));
    }

    [Test]
    public async Task UpdateTrade_UsesFaceValueAtTradeDateInsteadOfCurrentBondNominal()
    {
        var tradeProvider = new CapturingTradesProvider();
        var bondsService = new HistoricalBondsService(500);
        var controller = new TradesController(
            new PortfolioTradesService(tradeProvider, bondsService, new EmptyMoexHttpClient()));
        var request = new UpsertPortfolioTradeRequest(
            "RU000A", "TQCB", "RUB", new DateOnly(2024, 6, 20), TradeSide.Buy,
            1, 500, 1_000, 0, 0);

        await controller.UpdateTrade(Guid.NewGuid(), request, CancellationToken.None);

        Assert.That(tradeProvider.UpdatedTrade?.FaceValue, Is.EqualTo(500));
        Assert.That(bondsService.Requests.Single().TradeDate, Is.EqualTo(new DateOnly(2024, 6, 20)));
    }

    [Test]
    public async Task AddTrade_WhenHistoryHasNoFaceValue_KeepsSubmittedFaceValue()
    {
        var tradeProvider = new CapturingTradesProvider();
        var controller = new TradesController(
            new PortfolioTradesService(
                tradeProvider, new HistoricalBondsService(null), new EmptyMoexHttpClient()));
        var request = new UpsertPortfolioTradeRequest(
            "RU000A", "TQCB", "RUB", new DateOnly(2024, 6, 20), TradeSide.Buy,
            1, 500, 1_000, 0, 0);

        await controller.AddTrade(request, CancellationToken.None);

        Assert.That(tradeProvider.AddedTrade?.FaceValue, Is.EqualTo(1_000));
    }

    [Test]
    public void AddTrade_WhenProviderRejectsOversell_PropagatesException()
    {
        var controller = new TradesController(
            new PortfolioTradesService(
                new RejectingTradesProvider(), new HistoricalBondsService(null), new EmptyMoexHttpClient()));
        var request = new UpsertPortfolioTradeRequest(
            "RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Sell,
            5, 99, 1_000, 0, 0);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.AddTrade(request, CancellationToken.None));
    }

    [Test]
    public void UpdateTrade_WhenProviderRejectsOversell_PropagatesException()
    {
        var controller = new TradesController(
            new PortfolioTradesService(
                new RejectingTradesProvider(), new HistoricalBondsService(null), new EmptyMoexHttpClient()));
        var request = new UpsertPortfolioTradeRequest(
            "RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Sell,
            5, 99, 1_000, 0, 0);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.UpdateTrade(Guid.NewGuid(), request, CancellationToken.None));
    }

    [Test]
    public void DeleteTrade_WhenProviderRejectsInvalidSequence_PropagatesException()
    {
        var controller = new TradesController(
            new PortfolioTradesService(
                new RejectingTradesProvider(), new HistoricalBondsService(null), new EmptyMoexHttpClient()));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.DeleteTrade(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class HistoricalBondsService(double? faceValue) : IBondsService
    {
        public List<BondFaceValueRequest> Requests { get; } = [];

        public Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
            IReadOnlyList<BondFaceValueRequest> requests,
            CancellationToken cancellationToken)
        {
            Requests.AddRange(requests);
            return Task.FromResult<IReadOnlyList<BondFaceValueResult>>(requests.Select(request =>
                new BondFaceValueResult(request.SecId, request.BoardId, request.TradeDate, faceValue)).ToArray());
        }

        public Task<IReadOnlyList<BondSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingTradesProvider : IPortfolioTradesProvider
    {
        public PortfolioTrade? AddedTrade { get; private set; }
        public PortfolioTrade? UpdatedTrade { get; private set; }

        public Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken)
        {
            AddedTrade = trade;
            return Task.FromResult(new PortfolioTradeRecord(
                Guid.NewGuid(), trade.SecId, trade.BoardId, trade.CurrencyId, trade.TradeDate,
                trade.Side, trade.Quantity, trade.Price, trade.FaceValue, trade.AccruedInterest,
                trade.CommissionAmount, trade.CommissionPercent, trade.Amount));
        }

        public Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken)
        {
            UpdatedTrade = trade;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RejectingTradesProvider : IPortfolioTradesProvider
    {
        private static InvalidOperationException OversellError() =>
            new("Количество продажи не может превышать доступный остаток на дату сделки.");

        public Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken) =>
            throw OversellError();

        public Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken) =>
            throw OversellError();

        public Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken) =>
            throw OversellError();
    }

    private sealed class EmptyMoexHttpClient : IMoexHttpClient
    {
        public Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexBond>>());

        public Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(string secId, string boardId, DateOnly? from = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
