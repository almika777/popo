using NUnit.Framework;
using Popo.Api.Models;
using Popo.Api.Services.BrokerReports;
using Popo.Core.Bonds;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class BrokerReportImportFaceValueTests
{
    [Test]
    public async Task Preview_UsesHistoricalFaceValue_AndImportPersistsThatValue()
    {
        var tradeDate = new DateOnly(2024, 6, 20);
        var parsedTrade = new ParsedBrokerTrade(
            tradeDate, new TimeOnly(10, 30), "123456", "RU000A",
            TradeSide.Buy, 50, "%", 1, 500, 0, 500, "RUB", 0, 0, 0, "TQCB");
        var bondsService = new HistoricalBondsService(500);
        var importProvider = new CapturingBrokerReportImportProvider();
        var service = new BrokerReportImportService(
            new FixedBrokerReportParser(new ParsedBrokerReport([parsedTrade], [], 0)),
            bondsService,
            new EmptyMoneyMarketFundsProvider(),
            new EmptyTradesProvider(),
            new EmptyCashFlowsProvider(),
            importProvider,
            new BrokerReportFaceValueResolver(bondsService));

        var preview = await service.PreviewAsync(new MemoryStream(), CancellationToken.None);
        var operation = preview.Operations.Single();
        var imported = await service.ImportAsync(new BrokerReportImportRequest(
        [
            new BrokerReportOperationRequest(
                operation.Id, operation.Kind, operation.Date, operation.SecId, operation.BoardId,
                operation.CurrencyId, operation.Side, operation.Quantity, operation.UnitPrice,
                operation.FaceValue, operation.AccruedInterestTotal, operation.Commission, operation.Amount)
        ]), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(operation.FaceValue, Is.EqualTo(500));
            Assert.That(imported.TradesAdded, Is.EqualTo(1));
            Assert.That(importProvider.Batch?.Trades.Single().Operation.FaceValue, Is.EqualTo(500));
        });
    }

    private sealed class FixedBrokerReportParser(ParsedBrokerReport report) : IBrokerReportPdfParser
    {
        public ParsedBrokerReport Parse(Stream pdfStream) => report;
    }

    private sealed class HistoricalBondsService(double faceValue) : IBondsService
    {
        public Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
            IReadOnlyList<BondFaceValueRequest> requests,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BondFaceValueResult>>(requests.Select(request =>
                new BondFaceValueResult(request.SecId, request.BoardId, request.TradeDate, faceValue)).ToArray());

        public Task<IReadOnlyList<BondSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BondSearchResult>>(
                [new BondSearchResult("RU000A", "TQCB", "RUB", "Bond", 0, 1_000)]);

        public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class EmptyMoneyMarketFundsProvider : IPortfolioMoneyMarketFundsProvider
    {
        public Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MoneyMarketFundRecord>>([]);

        public Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MoneyMarketFundOperationRecord>>([]);

        public Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(
            MoneyMarketFundOperation operation,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptyTradesProvider : IPortfolioTradesProvider
    {
        public Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioTradeRecord>>([]);

        public Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyCashFlowsProvider : IPortfolioCashFlowsProvider
    {
        public Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioCashFlowRecord>>([]);

        public Task<PortfolioCashFlowRecord?> GetCashFlowAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioCashFlowRecord> AddCashFlowAsync(
            DateOnly date, PortfolioCashFlowType type, double amount, string comment, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> UpdateCashFlowAsync(
            Guid id, DateOnly date, PortfolioCashFlowType type, double amount, string comment, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingBrokerReportImportProvider : IBrokerReportImportProvider
    {
        public BrokerReportImportBatch? Batch { get; private set; }

        public Task<BrokerReportImportResult> AddAsync(
            BrokerReportImportBatch batch,
            CancellationToken cancellationToken)
        {
            Batch = batch;
            return Task.FromResult(new BrokerReportImportResult(batch.Trades.Count, 0, 0));
        }
    }
}
