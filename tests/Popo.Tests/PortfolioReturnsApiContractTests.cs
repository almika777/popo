using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Popo.Api;
using Popo.Api.Controllers;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Api.Services.Position;
using Popo.Core.Contracts.Iss;
using Popo.Core.Common;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class PortfolioReturnsApiContractTests
{
    [Test]
    public async Task ValuationValidator_RejectsNegativeValueWithRussianMessage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POPO_DB_CONNECTION_STRING"] = "Host=127.0.0.1;Database=unused;Username=unused;Password=unused"
            })
            .Build();
        using var services = new ServiceCollection()
            .AddPopoApiServices(configuration)
            .BuildServiceProvider();
        var validator = services.GetRequiredService<IValidator<UpsertPortfolioValuationRequest>>();

        var result = await validator.ValidateAsync(
            new UpsertPortfolioValuationRequest(new DateOnly(2026, 1, 1), -1, null));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Select(error => error.ErrorMessage),
            Does.Contain("Стоимость портфеля должна быть неотрицательной."));
    }

    [Test]
    public void AddValuation_RejectsDuplicateDate()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        var service = CreateDataService(provider);

        var exception = Assert.ThrowsAsync<ApplicationConflictException>(async () => await service.AddValuationAsync(
            new UpsertPortfolioValuationRequest(new DateOnly(2026, 1, 1), 120_000, null),
            CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("уже существует"));
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
            new PortfolioReturnQuery { From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 2, 1) },
            CancellationToken.None);

        var response = result.Result as OkObjectResult;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Value, Is.TypeOf<PortfolioReturnResult>());
        Assert.That(((PortfolioReturnResult)response.Value!).PeriodReturn, Is.EqualTo(0.1).Within(0.000001));
    }

    [Test]
    public async Task CalculateReturn_ReturnsNetReturnsAndTaxesAsMetadata()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 2, 1), 109_500, string.Empty));
        provider.CashFlows.Add(new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), PortfolioCashFlowType.Tax, 500, "Налог за 2025 год"));
        var controller = CreateController(provider);

        var result = await controller.CalculateReturn(
            new PortfolioReturnQuery { From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 2, 1) },
            CancellationToken.None);

        var response = result.Result as OkObjectResult;
        Assert.That(response, Is.Not.Null);
        var returnResult = (PortfolioReturnResult)response!.Value!;
        Assert.That(returnResult.Taxes, Is.EqualTo(500).Within(0.001));
        Assert.That(returnResult.AbsoluteReturn, Is.EqualTo(9_500).Within(0.001));
        Assert.That(returnResult.PeriodReturn, Is.EqualTo(0.095).Within(0.000001));
        Assert.That(returnResult.AnnualizedReturn, Is.EqualTo(Math.Pow(1.095, 365d / 31) - 1).Within(0.000001));
    }

    [Test]
    public void CalculateReturn_ReportsPreviousDayDatesMissingForCashFlows()
    {
        var provider = new FakeProvider();
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), 100_000, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 2, 1), 110_000, string.Empty));
        provider.CashFlows.Add(new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), PortfolioCashFlowType.Withdrawal, 10_000, string.Empty));
        var service = CreateDataService(provider);

        var exception = Assert.ThrowsAsync<InvalidApplicationRequestException>(async () => await service.CalculateReturnAsync(
            new PortfolioReturnQuery { From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 2, 1) },
            CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("2026-01-04"));
    }

    [Test]
    public async Task CalculateBenchmark_ReturnsPortfolioAndLqdtScenarioForSamePeriod()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 4), 110, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), 130, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 140, string.Empty));
        provider.CashFlows.Add(new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), PortfolioCashFlowType.Deposit, 20, string.Empty));
        var history = new MoexIndexHistory[]
        {
            new() { SecId = "LQDT", TradeDate = new DateOnly(2025, 12, 31), Close = 1m },
            new() { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 4), Close = 1.1m },
            new() { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 5), Close = 1.1m },
            new() { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 6), Close = 1.2m },
            new() { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 20), Close = 1.2m },
            new() { SecId = "LQDT", TradeDate = new DateOnly(2026, 2, 2), Close = 1.2m }
        };
        var controller = CreateController(provider, history);

        var result = await controller.CalculateBenchmark(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        var response = result.Result as OkObjectResult;
        Assert.That(response, Is.Not.Null);
        var benchmark = response!.Value as PortfolioBenchmarkResult;
        Assert.That(benchmark, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(benchmark!.PortfolioReturn, Is.EqualTo(0.18461538461538463).Within(0.000001));
            Assert.That(benchmark.LqdtReturn, Is.EqualTo(1.2d / 1.1d - 1).Within(0.000001));
            Assert.That(benchmark.PortfolioProfitPercent, Is.EqualTo(0.18461538461538463).Within(0.000001));
            Assert.That(benchmark.LqdtProfitPercent, Is.EqualTo(1.2d / 1.1d - 1).Within(0.000001));
            Assert.That(benchmark.PortfolioPoints[^1].ProfitPercent, Is.EqualTo(benchmark.PortfolioProfitPercent));
            Assert.That(benchmark.LqdtPoints[^1].ProfitPercent, Is.EqualTo(benchmark.LqdtProfitPercent));
        });
    }

    [Test]
    public async Task CalculateBenchmark_StartsLqdtSeriesAtFirstObservationInsideSelectedPeriod()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        var result = await CreateDataService(
                provider,
                [new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1.01m }],
                [new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1.01m }])
            .CalculateBenchmarkAsync(
                new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtStartObservationDate, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.LqdtProfitPercent, Is.Zero.Within(0.000001));
        });
    }

    [Test]
    public async Task CalculateBenchmark_RefreshesAndStoresLqdtPricesWhenStoredHistoryIsIncomplete()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        var downloadedHistory = new[]
        {
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2025, 12, 31), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 5), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 20), Close = 1.1m }
        };
        var store = new FakeMoexIndexHistoryStore([]);
        var service = new PortfolioDataService(
            provider,
            provider,
            new PortfolioReturnCalculator(),
            new PortfolioBenchmarkService(
                provider,
                provider,
                store,
                new EmptyMoexHttpClient(downloadedHistory),
                new PortfolioBenchmarkCalculator()));

        var result = await service.CalculateBenchmarkAsync(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(1.1d, 1.8d) - 1d).Within(0.000001));
            Assert.That(store.UpsertCount, Is.EqualTo(1));
            Assert.That(store.History, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task CalculateBenchmark_RefreshesCachedHistoryWhenSelectedPeriodEndIsNotCovered()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        var cachedHistory = new[]
        {
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2025, 12, 31), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 20), Close = 1.1m }
        };
        var downloadedHistory = cachedHistory.Concat(new[]
        {
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 30), Close = 1.2m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 2, 2), Close = 1.21m }
        }).ToArray();
        var store = new FakeMoexIndexHistoryStore(cachedHistory);
        var moexClient = new EmptyMoexHttpClient(downloadedHistory);
        var service = new PortfolioDataService(
            provider,
            provider,
            new PortfolioReturnCalculator(),
            new PortfolioBenchmarkService(
                provider,
                provider,
                store,
                moexClient,
                new PortfolioBenchmarkCalculator()));

        var result = await service.CalculateBenchmarkAsync(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(moexClient.LqdtHistoryRequestCount, Is.EqualTo(1));
            Assert.That(store.UpsertCount, Is.EqualTo(1));
            var expectedReturn = 1.2d * Math.Pow(1.21d / 1.2d, 2d / 3d) - 1d;
            Assert.That(result.LqdtReturn, Is.EqualTo(expectedReturn).Within(0.000001));
            Assert.That(result.LqdtEndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 30)));
            Assert.That(result.LqdtPoints[^1].Date, Is.EqualTo(to));
            Assert.That(result.LqdtPoints[^1].ProfitPercent, Is.EqualTo(expectedReturn).Within(0.000001));
        });
    }

    [Test]
    public async Task CalculateBenchmark_RefreshesCachedHistoryWhenItMissesLatestQuoteInsidePeriod()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        var cachedHistory = new[]
        {
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 2, 2), Close = 1.21m }
        };
        var downloadedHistory = new[]
        {
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 30), Close = 1.2m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 2, 2), Close = 1.21m }
        };
        var store = new FakeMoexIndexHistoryStore(cachedHistory);
        var moexClient = new EmptyMoexHttpClient(downloadedHistory);
        var service = new PortfolioDataService(
            provider,
            provider,
            new PortfolioReturnCalculator(),
            new PortfolioBenchmarkService(
                provider,
                provider,
                store,
                moexClient,
                new PortfolioBenchmarkCalculator()));

        var result = await service.CalculateBenchmarkAsync(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(moexClient.LqdtHistoryRequestCount, Is.EqualTo(1));
            var expectedReturn = 1.2d * Math.Pow(1.21d / 1.2d, 2d / 3d) - 1d;
            Assert.That(result.LqdtReturn, Is.EqualTo(expectedReturn).Within(0.000001));
            Assert.That(result.LqdtEndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 30)));
        });
    }

    [Test]
    public async Task CalculateBenchmark_ReturnsLqdtWhenCashFlowDateValuationIsMissing()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        provider.CashFlows.Add(new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 5), PortfolioCashFlowType.Deposit, 10, string.Empty));
        var service = CreateDataService(provider,
        [
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 20), Close = 1.1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 2, 2), Close = 1.1m }
        ]);

        var result = await service.CalculateBenchmarkAsync(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtReturn, Is.EqualTo(0.1d).Within(0.000001));
            Assert.That(result.LqdtStartObservationDate, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.LqdtEndObservationDate, Is.EqualTo(new DateOnly(2026, 1, 20)));
            Assert.That(result.LqdtPoints[^1].Date, Is.EqualTo(to));
            Assert.That(result.PortfolioReturn, Is.Null);
            Assert.That(result.PortfolioProfitPercent, Is.Null);
            Assert.That(result.PortfolioPoints, Is.Empty);
            Assert.That(result.PortfolioUnavailableReason, Does.Contain("2026-01-05"));
        });
    }

    [Test]
    public async Task CalculateBenchmark_ReturnsLqdtWhenPortfolioEndValuationIsMissing()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(
            Guid.NewGuid(), new DateOnly(2026, 1, 20), 105, string.Empty));
        var service = CreateDataService(provider,
        [
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 30), Close = 1.1m }
        ]);

        var result = await service.CalculateBenchmarkAsync(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.LqdtReturn, Is.EqualTo(Math.Pow(1.1d, 30d / 28d) - 1d).Within(0.000001));
            Assert.That(result.LqdtPoints[0].Date, Is.EqualTo(new DateOnly(2026, 1, 2)));
            Assert.That(result.LqdtPoints[^1].Date, Is.EqualTo(to));
            Assert.That(result.PortfolioReturn, Is.Null);
            Assert.That(result.PortfolioProfitPercent, Is.Null);
            Assert.That(result.PortfolioPoints, Is.Empty);
            Assert.That(result.PortfolioUnavailableReason, Does.Contain("01.02.2026"));
        });
    }

    [Test]
    public void CalculateBenchmark_DoesNotTreatIndexStoreFailureAsInvalidRequest()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        var returnCalculator = new PortfolioReturnCalculator();
        var benchmarkService = new PortfolioBenchmarkService(
            provider,
            provider,
            new FailingMoexIndexHistoryStore(),
            new EmptyMoexHttpClient(
                [
                    new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 2), Close = 1m },
                    new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 30), Close = 1.1m }
                ]),
            new PortfolioBenchmarkCalculator());
        var service = new PortfolioDataService(provider, provider, returnCalculator, benchmarkService);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.CalculateBenchmarkAsync(
            new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None));
    }

    [Test]
    public void CalculateBenchmark_DoesNotTreatMalformedIndexHistoryAsInvalidRequest()
    {
        var provider = new FakeProvider();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 2, 1);
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), from, 100, string.Empty));
        provider.Valuations.Add(new PortfolioValuationRecord(Guid.NewGuid(), to, 110, string.Empty));
        var service = CreateDataService(provider,
        [
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2025, 12, 31), Close = 1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 20), Close = 1.1m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 1, 20), Close = 1.2m },
            new MoexIndexHistory { SecId = "LQDT", TradeDate = new DateOnly(2026, 2, 2), Close = 1.1m }
        ]);

        Assert.ThrowsAsync<InvalidPortfolioBenchmarkHistoryException>(async () =>
            await service.CalculateBenchmarkAsync(
                new PortfolioReturnQuery { From = from, To = to }, CancellationToken.None));
    }

    private static PortfolioController CreateController() => CreateController(new FakeProvider());

    private static PortfolioController CreateController(
        FakeProvider provider,
        IReadOnlyCollection<MoexIndexHistory>? indexHistory = null)
    {
        var positionsService = new FakePositionsService();
        var returnCalculator = new PortfolioReturnCalculator();
        var benchmarkCalculator = new PortfolioBenchmarkCalculator();
        var benchmarkService = new PortfolioBenchmarkService(
            provider,
            provider,
            new FakeMoexIndexHistoryStore(indexHistory ?? []),
            new EmptyMoexHttpClient(indexHistory),
            benchmarkCalculator);
        return new PortfolioController(
            new PortfolioDataService(provider, provider, returnCalculator, benchmarkService),
            new PortfolioPageService(provider, provider, positionsService),
            null!);
    }

    private static PortfolioDataService CreateDataService(
        FakeProvider provider,
        IReadOnlyCollection<MoexIndexHistory>? indexHistory = null,
        IReadOnlyCollection<MoexIndexHistory>? downloadedHistory = null)
    {
        var returnCalculator = new PortfolioReturnCalculator();
        var benchmarkService = new PortfolioBenchmarkService(
            provider,
            provider,
            new FakeMoexIndexHistoryStore(indexHistory ?? []),
            new EmptyMoexHttpClient(downloadedHistory ?? indexHistory),
            new PortfolioBenchmarkCalculator());
        return new PortfolioDataService(provider, provider, returnCalculator, benchmarkService);
    }

    private sealed class FakeMoexIndexHistoryStore : IMoexIndexHistoryStore
    {
        public FakeMoexIndexHistoryStore(IReadOnlyCollection<MoexIndexHistory> history) => History = history.ToList();

        public List<MoexIndexHistory> History { get; }
        public int UpsertCount { get; private set; }

        public Task UpsertAsync(IReadOnlyCollection<MoexIndexHistory> values, CancellationToken cancellationToken)
        {
            UpsertCount++;
            foreach (var value in values)
            {
                History.RemoveAll(existing => existing.SecId == value.SecId && existing.TradeDate == value.TradeDate);
                History.Add(value);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MoexIndexHistory>> GetHistoryAsync(
            string secId,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MoexIndexHistory>>(History
                .Where(x => x.SecId == secId && x.TradeDate >= from && x.TradeDate <= to)
                .ToArray());
    }

    private sealed class FailingMoexIndexHistoryStore : IMoexIndexHistoryStore
    {
        public Task UpsertAsync(IReadOnlyCollection<MoexIndexHistory> values, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Index store failed.");

        public Task<IReadOnlyList<MoexIndexHistory>> GetHistoryAsync(
            string secId,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MoexIndexHistory>>([]);
    }

    private sealed class EmptyMoexHttpClient(IReadOnlyCollection<MoexIndexHistory>? lqdtHistory = null) : IMoexHttpClient
    {
        public int LqdtHistoryRequestCount { get; private set; }
        public Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexBond>>());

        public Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(
            string secId, string boardId, DateOnly? from = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexMarketdataYields>>());

        public Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexMarketdata>>());

        public Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<MoexIndexHistory>> GetLqdtIndexHistoryAsync(CancellationToken ct)
        {
            LqdtHistoryRequestCount++;
            return Task.FromResult(lqdtHistory?.ToList() ?? []);
        }

        public Task<List<MoexIndexHistory>> GetLqdtPriceHistoryAsync(CancellationToken ct)
        {
            LqdtHistoryRequestCount++;
            return Task.FromResult(lqdtHistory?.Where(value => value.SecId == "LQDT").ToList() ?? []);
        }

        public Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct) =>
            Task.FromResult(new List<SecDescription>());

        public Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct) =>
            Task.FromResult(new List<MoexEmitentDescription>());
    }

    private sealed class FakePositionsService(PortfolioOverview? overview = null) : IPortfolioPositionsService
    {
        public Task<PositionsPageResponse> GetPageAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PortfolioOverview> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken) =>
            Task.FromResult(overview ?? throw new NotSupportedException());

        public Task<PortfolioSummaryRecord> GetSummaryAsync(DateOnly asOf, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PortfolioCurrencyAnalyticsAsset>> GetCurrencyAnalyticsAssetsAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProvider : IPortfolioValuationsProvider, IPortfolioCashFlowsProvider
    {
        public List<PortfolioValuationRecord> Valuations { get; } = [];
        public List<PortfolioCashFlowRecord> CashFlows { get; } = [];
        public Task<IReadOnlyList<PortfolioValuationRecord>> GetValuationsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PortfolioValuationRecord>>(Valuations);
        }

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

        public Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PortfolioCashFlowRecord>>(CashFlows);
        }

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
