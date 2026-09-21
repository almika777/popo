using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Popo.Api.Controllers;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class PortfolioReturnsApiContractTests
{
    [Test]
    public async Task AddValuation_RejectsNegativeValue()
    {
        var controller = CreateController();

        var result = await controller.AddValuation(
            new UpsertPortfolioValuationRequest(new DateOnly(2026, 1, 1), -1, null),
            CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task AddValuation_RejectsDuplicateDate()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        var controller = CreateController(provider);

        var result = await controller.AddValuation(
            new UpsertPortfolioValuationRequest(new DateOnly(2026, 1, 1), 120_000, null),
            CancellationToken.None);

        Assert.That(result.Result, Is.TypeOf<ConflictObjectResult>());
    }

    [Test]
    public async Task UpdateValuation_ChangesRequestedRecord()
    {
        var provider = new FakeProvider();
        var id = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 10);
        provider.Valuations.Add(new PortfolioValuationRecord(id, date, 4_763_693, "Контрольная оценка"));
        provider.Valuations.Add(new PortfolioValuationRecord(
            otherId, new DateOnly(2026, 8, 9), 4_771_353, "Предыдущая оценка"));
        var controller = CreateController(provider);

        var result = await controller.UpdateValuation(
            id,
            new UpsertPortfolioValuationRequest(date, 4_800_000, "Контрольная оценка"),
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        Assert.That(provider.Valuations.Single(x => x.Id == id), Is.EqualTo(
            new PortfolioValuationRecord(id, date, 4_800_000, "Контрольная оценка")));
        Assert.That(provider.Valuations.Single(x => x.Id == otherId), Is.EqualTo(
            new PortfolioValuationRecord(otherId, new DateOnly(2026, 8, 9), 4_771_353, "Предыдущая оценка")));
    }

    [Test]
    public async Task CalculateReturn_ReturnsCalculatedResult()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 2, 1), 110_000, string.Empty));
        var controller = CreateController(provider);

        var result = await controller.CalculateReturn(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            CancellationToken.None);

        var response = result.Result as OkObjectResult;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Value, Is.TypeOf<PortfolioReturnResult>());
        Assert.That(((PortfolioReturnResult)response.Value!).PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public async Task CalculateReturn_IncludesTaxesSeparately()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 2, 1), 110_000, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 4), 110_000, string.Empty));
        provider.CashFlows.Add(new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), PortfolioCashFlowType.Tax, 10_000, "Налог за 2025 год"));
        var controller = CreateController(provider);

        var result = await controller.CalculateReturn(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            CancellationToken.None);

        var response = result.Result as OkObjectResult;
        Assert.That(response, Is.Not.Null);
        var returnResult = (PortfolioReturnResult)response!.Value!;
        Assert.That(returnResult.Taxes, Is.EqualTo(10_000).Within(0.001));
        Assert.That(returnResult.AbsoluteReturn, Is.EqualTo(20_000).Within(0.001));
    }

    [Test]
    public async Task CalculateReturn_ReportsPreviousDayDatesMissingForCashFlows()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 2, 1), 110_000, string.Empty));
        provider.CashFlows.Add(new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), PortfolioCashFlowType.Withdrawal, 10_000, string.Empty));
        var controller = CreateController(provider);

        var result = await controller.CalculateReturn(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            CancellationToken.None);

        var response = result.Result as BadRequestObjectResult;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Value, Does.Contain("2026-01-04"));
    }

    private static PortfolioController CreateController() => CreateController(new FakeProvider());

    private static PortfolioController CreateController(FakeProvider provider)
    {
        var positionsService = new FakePositionsService();
        return new PortfolioController(
            provider,
            new PortfolioReturnCalculator(),
            new PortfolioPageService(provider, positionsService));
    }

    private sealed class FakePositionsService : IPortfolioPositionsService
    {
        public Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioSummaryRecord> GetSummaryAsync(DateOnly asOf, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProvider : IPortfolioReturnInputsProvider
    {
        public List<PortfolioValuationRecord> Valuations { get; } = [];
        public List<PortfolioCashFlowRecord> CashFlows { get; } = [];

        public Task<IReadOnlyList<PortfolioValuationRecord>> GetValuationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioValuationRecord>>(Valuations);

        public Task<PortfolioValuationRecord?> GetValuationAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Valuations.SingleOrDefault(x => x.Id == id));

        public Task<PortfolioValuationRecord> AddValuationAsync(DateOnly date, double totalValue, string comment, CancellationToken cancellationToken)
        {
            var record = new PortfolioValuationRecord(Guid.NewGuid(), date, totalValue, comment);
            Valuations.Add(record);
            return Task.FromResult(record);
        }

        public Task<bool> UpdateValuationAsync(Guid id, DateOnly date, double totalValue, string comment, CancellationToken cancellationToken)
        {
            var index = Valuations.FindIndex(x => x.Id == id);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            Valuations[index] = new PortfolioValuationRecord(id, date, totalValue, comment);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteValuationAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioCashFlowRecord>>(CashFlows);

        public Task<PortfolioCashFlowRecord?> GetCashFlowAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(CashFlows.SingleOrDefault(x => x.Id == id));

        public Task<PortfolioCashFlowRecord> AddCashFlowAsync(DateOnly date, PortfolioCashFlowType type, double amount, string comment, CancellationToken cancellationToken)
        {
            var record = new PortfolioCashFlowRecord(Guid.NewGuid(), date, type, amount, comment);
            CashFlows.Add(record);
            return Task.FromResult(record);
        }

        public Task<bool> UpdateCashFlowAsync(Guid id, DateOnly date, PortfolioCashFlowType type, double amount, string comment, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
