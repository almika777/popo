using NUnit.Framework;
using Popo.Api.Services;
using Popo.Api.Services.Statistics;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Statistics;

namespace Popo.Tests;

public sealed class BondDailyStatisticsServiceTests
{
    [Test]
    public void GetDailyStatisticsAsync_ExposesEditableLiquidityThresholds()
    {
        var parameterNames = typeof(BondDailyStatisticsService)
            .GetMethod(nameof(BondDailyStatisticsService.GetDailyStatisticsAsync))!
            .GetParameters()
            .Select(parameter => parameter.Name)
            .ToArray();

        Assert.That(parameterNames, Does.Contain("minimumTrades"));
        Assert.That(parameterNames, Does.Contain("minimumTurnoverRub"));
    }

    [Test]
    public async Task GetDailyStatisticsAsync_HistoricalDateUsesSelectedAndPreviousTradingCloses()
    {
        var previousDate = new DateOnly(2026, 10, 26);
        var selectedDate = new DateOnly(2026, 10, 27);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("BOND1", 100, emitterId: 11004), Price("BOND2", 100)],
            [selectedDate] = [Price("BOND1", 90, emitterId: 11004), Price("BOND2", 110)]
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2));

        var result = await service.GetDailyStatisticsAsync(selectedDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Date, Is.EqualTo(selectedDate));
            Assert.That(result.ComparedWithDate, Is.EqualTo(previousDate));
            Assert.That(result.Declines.Single().ChangePercent, Is.EqualTo(-10));
            Assert.That(result.Increases.Single().ChangePercent, Is.EqualTo(10));
            Assert.That(result.Declines.Single().EmitterId, Is.EqualTo(11004));
            Assert.That(result.Turnovers.Single(item => item.SecId == "BOND1").EmitterId, Is.EqualTo(11004));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_AppliesLiquidityThresholdsOnlyToSelectedSession()
    {
        var previousDate = new DateOnly(2026, 10, 26);
        var selectedDate = new DateOnly(2026, 10, 27);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] =
            [
                Price("RUB-PASS", 100),
                Price("CNY-PASS", 100, boardId: "TQOY", currencyId: "CNY", turnover: 85_000),
                Price("LOW-TRADES-PREVIOUS", 100, numTrades: 4),
                Price("LOW-TURNOVER-PREVIOUS", 100, turnover: 999_999),
                Price("LOW-TRADES-CURRENT", 100),
                Price("LOW-TURNOVER-CURRENT", 100)
            ],
            [selectedDate] =
            [
                Price("RUB-PASS", 90),
                Price("CNY-PASS", 110, boardId: "TQOY", currencyId: "CNY", turnover: 85_000),
                Price("LOW-TRADES-PREVIOUS", 90),
                Price("LOW-TURNOVER-PREVIOUS", 90),
                Price("LOW-TRADES-CURRENT", 90, numTrades: 4),
                Price("LOW-TURNOVER-CURRENT", 90, turnover: 999_999)
            ]
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2), new Dictionary<string, double> { ["CNY"] = 12 });

        var result = await service.GetDailyStatisticsAsync(
            selectedDate,
            CancellationToken.None,
            minimumTrades: 5,
            minimumTurnoverRub: 1_000_000);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines.Select(item => item.SecId), Is.EqualTo(new[]
            {
                "LOW-TRADES-PREVIOUS",
                "LOW-TURNOVER-PREVIOUS",
                "RUB-PASS"
            }));
            Assert.That(result.Increases.Select(item => item.SecId), Is.EqualTo(new[] { "CNY-PASS" }));
            Assert.That(result.Increases.Single().ChangePercent, Is.EqualTo(10));
            Assert.That(result.Declines.Select(item => item.SecId), Does.Not.Contain("LOW-TRADES-CURRENT"));
            Assert.That(result.Declines.Select(item => item.SecId), Does.Not.Contain("LOW-TURNOVER-CURRENT"));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_RanksSelectedSessionTurnoverInRublesAfterLiquidityFilters()
    {
        var previousDate = new DateOnly(2026, 10, 26);
        var selectedDate = new DateOnly(2026, 10, 27);
        BondDailyPricePoint[] selectedPrices =
        [
            Price("RUB-2M", 100, turnover: 2_000_000),
            Price("RUB-6M", 100, turnover: 6_000_000),
            Price("CNY-3M", 100, boardId: "TQOY", currencyId: "CNY", turnover: 3_000_000),
            Price("RUB-5M", 100, turnover: 5_000_000),
            Price("RUB-4M", 100, turnover: 4_000_000),
            Price("RUB-3M", 100, turnover: 3_000_000),
            Price("LOW-TRADES", 100, numTrades: 4, turnover: 100_000_000),
            Price("LOW-TURNOVER", 100, turnover: 999_999)
        ];
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = selectedPrices,
            [selectedDate] = selectedPrices
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2), new Dictionary<string, double> { ["CNY"] = 12 });

        var result = await service.GetDailyStatisticsAsync(selectedDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Turnovers.Select(item => item.SecId), Is.EqualTo(new[] { "CNY-3M", "RUB-6M", "RUB-5M", "RUB-4M", "RUB-3M" }));
            Assert.That(result.Turnovers.Select(item => item.TurnoverRub), Is.EqualTo(new[] { 36_000_000, 6_000_000, 5_000_000, 4_000_000, 3_000_000 }));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_FiltersAllListsByIssuerCategory()
    {
        var previousDate = new DateOnly(2026, 10, 26);
        var selectedDate = new DateOnly(2026, 10, 27);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] =
            [
                Price("GOV-DOWN", 100, turnover: 3_000_000, issuerCategory: BondIssuerCategory.Government),
                Price("GOV-UP", 100, turnover: 4_000_000, issuerCategory: BondIssuerCategory.Government),
                Price("MUNI-DOWN", 100, turnover: 5_000_000, issuerCategory: BondIssuerCategory.Municipal),
                Price("CORP-UP", 100, turnover: 6_000_000, issuerCategory: BondIssuerCategory.Corporate)
            ],
            [selectedDate] =
            [
                Price("GOV-DOWN", 90, turnover: 3_000_000, issuerCategory: BondIssuerCategory.Government),
                Price("GOV-UP", 110, turnover: 4_000_000, issuerCategory: BondIssuerCategory.Government),
                Price("MUNI-DOWN", 50, turnover: 5_000_000, issuerCategory: BondIssuerCategory.Municipal),
                Price("CORP-UP", 130, turnover: 6_000_000, issuerCategory: BondIssuerCategory.Corporate)
            ]
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2));
        var result = await service.GetDailyStatisticsAsync(
            selectedDate,
            CancellationToken.None,
            minimumTrades: 0,
            minimumTurnoverRub: 0,
            issuerCategory: BondIssuerCategory.Government);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines.Select(item => item.SecId), Is.EqualTo(new[] { "GOV-DOWN" }));
            Assert.That(result.Increases.Select(item => item.SecId), Is.EqualTo(new[] { "GOV-UP" }));
            Assert.That(result.Turnovers.Select(item => item.SecId), Is.EqualTo(new[] { "GOV-UP", "GOV-DOWN" }));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_CurrentDateAppliesIssuerCategoryToLiveQuotes()
    {
        var today = new DateOnly(2026, 11, 2);
        var previousDate = today.AddDays(-3);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] =
            [
                Price("GOV", 100, issuerCategory: BondIssuerCategory.Government, shortName: "Государственная облигация"),
                Price("CORP", 100, issuerCategory: BondIssuerCategory.Corporate)
            ]
        });
        var quotes = new Dictionary<string, List<MoexMarketdata>>
        {
            ["TQCB"] =
            [
                new() { SecId = "GOV", BoardId = "TQCB", Last = 90, NumTrades = 10, ValueToday = 2_000_000 },
                new() { SecId = "CORP", BoardId = "TQCB", Last = 80, NumTrades = 10, ValueToday = 3_000_000 }
            ]
        };
        var service = CreateService(history, quotes, today);

        var result = await service.GetDailyStatisticsAsync(
            today,
            CancellationToken.None,
            minimumTrades: 0,
            minimumTurnoverRub: 0,
            issuerCategory: BondIssuerCategory.Government);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines.Select(item => item.SecId), Is.EqualTo(new[] { "GOV" }));
            Assert.That(result.Turnovers.Select(item => item.SecId), Is.EqualTo(new[] { "GOV" }));
            Assert.That(result.Turnovers.Single().ShortName, Is.EqualTo("Государственная облигация"));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_ReportsInsufficientDataWhenNoPricesMatchByBondAndBoard()
    {
        var previousDate = new DateOnly(2026, 10, 26);
        var selectedDate = new DateOnly(2026, 10, 27);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("BOND1", 100)],
            [selectedDate] = [Price("BOND2", 90)]
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2));

        var result = await service.GetDailyStatisticsAsync(selectedDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines, Is.Empty);
            Assert.That(result.Increases, Is.Empty);
            Assert.That(result.ComparedWithDate, Is.Null);
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_KeepsComparisonDateWhenMatchingPricesHaveNoMovers()
    {
        var previousDate = new DateOnly(2026, 10, 26);
        var selectedDate = new DateOnly(2026, 10, 27);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("BOND1", 100)],
            [selectedDate] = [Price("BOND1", 100)]
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2));

        var result = await service.GetDailyStatisticsAsync(selectedDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines, Is.Empty);
            Assert.That(result.Increases, Is.Empty);
            Assert.That(result.ComparedWithDate, Is.EqualTo(previousDate));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_CurrentDateUsesLastTradePriceOnly()
    {
        var today = new DateOnly(2026, 11, 2);
        var previousDate = today.AddDays(-3);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("BOND1", 100), Price("BOND2", 100), Price("NO-LAST", 100), Price("STALE", 100)]
        });
        var quotes = new Dictionary<string, List<MoexMarketdata>>
        {
            ["TQCB"] =
            [
                new() { SecId = "BOND1", BoardId = "TQCB", Last = 95, NumTrades = 10, ValueToday = 1_500_000, LCurrentPrice = 83, MarketPrice2 = 80 },
                new() { SecId = "BOND2", BoardId = "TQCB", Last = 90, NumTrades = 10, ValueToday = 1_500_000, LCurrentPrice = 0, MarketPrice2 = 87, LClosePrice = 1 },
                new() { SecId = "NO-LAST", BoardId = "TQCB", LCurrentPrice = 85, MarketPrice2 = 80 },
                new() { SecId = "STALE", BoardId = "TQCB", LClosePrice = 1, ClosePrice = 1 }
            ]
        };
        var service = CreateService(history, quotes, today);

        var result = await service.GetDailyStatisticsAsync(today, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ComparedWithDate, Is.EqualTo(previousDate));
            Assert.That(result.Declines.Select(item => item.SecId), Is.EqualTo(new[] { "BOND2", "BOND1" }));
            Assert.That(result.Declines.Single(item => item.SecId == "BOND1").CurrentPrice, Is.EqualTo(95));
            Assert.That(result.Declines.Single(item => item.SecId == "BOND2").CurrentPrice, Is.EqualTo(90));
            Assert.That(result.Increases, Is.Empty);
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_CurrentDateConvertsLiveSettlementTurnoverToRubles()
    {
        var today = new DateOnly(2026, 11, 2);
        var previousDate = today.AddDays(-3);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("CNY-BOND", 100, boardId: "TQOY", currencyId: "CNY", turnover: 85_000, emitterId: 11004)]
        });
        var quotes = new Dictionary<string, List<MoexMarketdata>>
        {
            ["TQOY"] =
            [
                new() { SecId = "CNY-BOND", BoardId = "TQOY", Last = 110, NumTrades = 10, ValueToday = 85_000 }
            ]
        };
        var service = CreateService(history, quotes, today, new Dictionary<string, double> { ["CNY"] = 12 });

        var result = await service.GetDailyStatisticsAsync(today, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Increases.Select(item => item.SecId), Is.EqualTo(new[] { "CNY-BOND" }));
            Assert.That(result.Increases.Single().EmitterId, Is.EqualTo(11004));
            Assert.That(result.Turnovers.Single().SecId, Is.EqualTo("CNY-BOND"));
            Assert.That(result.Turnovers.Single().TurnoverRub, Is.EqualTo(1_020_000));
            Assert.That(result.Turnovers.Single().EmitterId, Is.EqualTo(11004));
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_ReturnsEmptyForWeekdayWithoutSelectedHistory()
    {
        var selectedDate = new DateOnly(2026, 10, 27);
        var previousDate = selectedDate.AddDays(-1);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("BOND1", 100)]
        });
        var service = CreateService(history, [], new DateOnly(2026, 11, 2));

        var result = await service.GetDailyStatisticsAsync(selectedDate, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines, Is.Empty);
            Assert.That(result.Increases, Is.Empty);
            Assert.That(result.ComparedWithDate, Is.Null);
        });
    }

    [Test]
    public async Task GetDailyStatisticsAsync_OmitsCurrentQuoteWhenOnlyClosePriceExists()
    {
        var today = new DateOnly(2026, 11, 2);
        var previousDate = today.AddDays(-3);
        var history = new FakeHistoryProvider(new Dictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>>
        {
            [previousDate] = [Price("BOND1", 100)]
        });
        var quotes = new Dictionary<string, List<MoexMarketdata>>
        {
            ["TQCB"] = [new() { SecId = "BOND1", BoardId = "TQCB", ClosePrice = 90 }]
        };
        var service = CreateService(history, quotes, today);

        var result = await service.GetDailyStatisticsAsync(today, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Declines, Is.Empty);
            Assert.That(result.Increases, Is.Empty);
            Assert.That(result.ComparedWithDate, Is.Null);
        });
    }

    private static BondDailyStatisticsService CreateService(
        IBondDailyPriceHistoryProvider history,
        Dictionary<string, List<MoexMarketdata>> quotes,
        DateOnly today,
        IReadOnlyDictionary<string, double>? ratesToRub = null)
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(3)));
        return new BondDailyStatisticsService(
            new FakeCurrencyRatesProvider(ratesToRub ?? new Dictionary<string, double>()),
            new BondDailyPriceComparisonCalculatorFactory(
                new CurrentBondDailyPriceComparisonCalculator(history, new FakeMoexHttpClient(quotes)),
                new HistoricalBondDailyPriceComparisonCalculator(history)),
            new BondDailyChangesCalculator(new(), new(), new()),
            new BondDailyTopTurnoversCalculator(),
            timeProvider);
    }

    private static BondDailyPricePoint Price(
        string secId,
        double value,
        double numTrades = 10,
        double turnover = 1_500_000,
        string boardId = "TQCB",
        string currencyId = "RUB",
        BondIssuerCategory? issuerCategory = null,
        string? shortName = null,
        int? emitterId = null) =>
        new(secId, boardId, shortName ?? secId, value, numTrades, turnover, currencyId, issuerCategory, emitterId);

    private sealed class FakeHistoryProvider(IReadOnlyDictionary<DateOnly, IReadOnlyList<BondDailyPricePoint>> prices)
        : IBondDailyPriceHistoryProvider
    {
        public Task<DateOnly?> GetLatestTradingDateBeforeAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(prices.Keys.Where(candidate => candidate < date).MaxBy(candidate => candidate) as DateOnly?);

        public Task<IReadOnlyList<BondDailyPricePoint>> GetClosingPricesAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(prices.TryGetValue(date, out var values) ? values : (IReadOnlyList<BondDailyPricePoint>)[]);

        public Task<IReadOnlyDictionary<string, BondDailySecurityMetadata>> GetSecurityMetadataAsync(
            IReadOnlyCollection<string> secIds,
            CancellationToken cancellationToken)
        {
            var metadata = prices.Values
                .SelectMany(points => points)
                .Where(point => secIds.Contains(point.SecId, StringComparer.OrdinalIgnoreCase))
                .GroupBy(point => point.SecId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new BondDailySecurityMetadata(group.First().ShortName, group.First().IssuerCategory, group.First().EmitterId),
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, BondDailySecurityMetadata>>(metadata);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private sealed class FakeCurrencyRatesProvider(IReadOnlyDictionary<string, double> ratesToRub)
        : IPortfolioCurrencyRatesProvider
    {
        public Task<IReadOnlyList<PortfolioCurrencyRateRecord>> GetLatestRatesAsync(
            IReadOnlyCollection<string> currencyIds,
            DateOnly asOf,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioCurrencyRateRecord>>(currencyIds
                .Where(ratesToRub.ContainsKey)
                .Select(currencyId => new PortfolioCurrencyRateRecord(currencyId, asOf, ratesToRub[currencyId]))
                .ToArray());

        public Task<IReadOnlyList<string>> GetAvailableCurrencyIdsAsync(DateOnly asOf, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMoexHttpClient(Dictionary<string, List<MoexMarketdata>> marketdata) : IMoexHttpClient
    {
        public Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataFreshAsync(CancellationToken ct) => Task.FromResult(marketdata);
        public Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(string secId, string boardId, DateOnly? from = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexIndexHistory>> GetLqdtIndexHistoryAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexIndexHistory>> GetLqdtPriceHistoryAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexIndexHistory>> GetMoneyMarketFundPriceHistoryAsync(string secId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
