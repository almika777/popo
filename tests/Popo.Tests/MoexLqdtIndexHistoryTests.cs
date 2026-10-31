using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using Popo.Core.PortfolioReturns;
using Popo.HttpClients;

namespace Popo.Tests;

[TestFixture]
public sealed class MoexLqdtIndexHistoryTests
{
    private static readonly DateOnly FirstCalculationDate = new(2022, 7, 22);
    private const int LqdtHistoryPageSize = 100;
    private const int LiveHistoryRows = 1_075;

    [Test]
    public async Task GetLqdtIndexHistoryAsync_requests_paginated_lqdt_prices_and_lqdtm_index_history()
    {
        var handler = new TestHttpMessageHandler(request =>
        {
            var start = GetStart(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath.Contains("/index/securities/LQDTM.json", StringComparison.Ordinal))
            {
                var date = start == 0 ? "2022-07-22" : "2022-07-25";
                var close = start == 0 ? 1.0000m : 1.0001m;
                return JsonResponse(HistoryPayload(
                    [[date, "LQDTM", close]], start, 2, 1,
                    ["TRADEDATE", "SECID", "CLOSE"]));
            }

            var priceDate = start == 0 ? "2022-07-22" : "2022-07-25";
            var legalClose = start == 0 ? 10.00m : 10.01m;
            return JsonResponse(HistoryPayload(
                [[priceDate, "LQDT", "TQTF", legalClose - 0.01m, legalClose]], start, 2, 1));
        });
        var client = CreateClient(handler);

        var result = await client.GetLqdtIndexHistoryAsync(CancellationToken.None);
        var lqdtPrices = result.Where(value => value.SecId == "LQDT").ToArray();
        var lqdtmIndex = result.Where(value => value.SecId == "LQDTM").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(4));
            Assert.That(handler.Requests[0], Does.Contain("/history/engines/stock/markets/index/securities/LQDTM.json"));
            Assert.That(handler.Requests[0], Does.Contain("from=2022-07-22"));
            Assert.That(handler.Requests[0], Does.Contain("start=0"));
            Assert.That(handler.Requests[1], Does.Contain("start=1"));
            Assert.That(handler.Requests[2], Does.Contain("/history/engines/stock/markets/shares/securities/LQDT.json"));
            Assert.That(Uri.UnescapeDataString(handler.Requests[2]),
                Does.Contain("history.columns=TRADEDATE,SECID,BOARDID,LEGALCLOSEPRICE"));
            Assert.That(handler.Requests[2], Does.Contain("from=2022-07-22"));
            Assert.That(handler.Requests[2], Does.Contain("start=0"));
            Assert.That(handler.Requests[3], Does.Contain("start=1"));
            Assert.That(lqdtmIndex.Select(value => value.TradeDate), Is.EqualTo(new DateOnly?[]
            {
                new(2022, 7, 22), new(2022, 7, 25)
            }));
            Assert.That(lqdtmIndex.Select(value => value.Close), Is.EqualTo(new decimal?[] { 1.0000m, 1.0001m }));
            Assert.That(lqdtPrices.Select(value => value.TradeDate), Is.EqualTo(new DateOnly?[]
            {
                new(2022, 7, 22), new(2022, 7, 25)
            }));
            Assert.That(lqdtPrices.Select(value => value.Close), Is.EqualTo(new decimal?[] { 10.00m, 10.01m }));
        });
    }

    [Test]
    public async Task GetLqdtPriceHistoryAsync_uses_paginated_official_closing_prices_only()
    {
        var handler = CreateTwoPageLqdtHistoryHandler();
        var result = await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests.All(request => request.Contains(
                "/history/engines/stock/markets/shares/securities/LQDT.json", StringComparison.Ordinal)), Is.True);
            Assert.That(handler.Requests.All(request => request.Contains("limit=100", StringComparison.Ordinal)), Is.True);
            Assert.That(result.Select(value => value.SecId), Is.All.EqualTo("LQDT"));
            Assert.That(result.Select(value => value.TradeDate), Is.EqualTo(new DateOnly?[]
            {
                new(2022, 7, 22), new(2022, 7, 25)
            }));
            Assert.That(result.Select(value => value.Close), Is.EqualTo(new decimal?[] { 10m, 10.01m }));
        });
    }

    [Test]
    public async Task GetMoneyMarketFundPriceHistoryAsync_loads_tmon_legal_closing_prices_from_first_trading_date()
    {
        var handler = new TestHttpMessageHandler(request => JsonResponse(HistoryPayload(
            [["2023-07-12", "TMON", "TQBR", 99.9m, 100m]], 0, 1, 1)));

        var result = await CreateClient(handler).GetMoneyMarketFundPriceHistoryAsync("TMON", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0], Does.Contain("/history/engines/stock/markets/shares/securities/TMON.json"));
            Assert.That(handler.Requests[0], Does.Contain("from=2023-07-12"));
            Assert.That(Uri.UnescapeDataString(handler.Requests[0]),
                Does.Contain("history.columns=TRADEDATE,SECID,BOARDID,LEGALCLOSEPRICE"));
            Assert.That(result.Select(value => value.SecId), Is.All.EqualTo("TMON"));
            Assert.That(result.Single().Close, Is.EqualTo(100m));
        });
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_a_partial_page_reported_by_cursor_as_complete()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload(
            [["2022-07-22", "LQDT", "TQTF", 9.99m, 10m]], 0, 101, 100)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("неполную страницу"));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_a_cursor_that_does_not_match_requested_page()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload(
            [["2022-07-22", "LQDT", "TQTF", 9.99m, 10m]], 1, 2, 1)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("некорректный курсор"));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task GetLqdtPriceHistoryAsync_accepts_an_empty_history_with_an_empty_cursor()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload([], 0, 0, 0)));

        var result = await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_an_empty_page_while_cursor_reports_remaining_rows()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload([], 0, 1, 1)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("неполную страницу"));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_an_empty_terminal_page_with_nonzero_page_size()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload([], 0, 0, 1)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("некорректный курсор"));
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_a_history_row_without_a_positive_legal_close()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload(
            [["2022-07-22", "LQDT", "TQTF", 10m, null]], 0, 1, 1)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("некорректную дневную цену"));
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_requires_cursor_to_guarantee_complete_history()
    {
        var history = new { columns = new[] { "TRADEDATE", "SECID", "BOARDID", "LEGALCLOSEPRICE" },
            data = new object[][] { ["2022-07-22", "LQDT", "TQTF", 10m] } };
        var handler = new TestHttpMessageHandler(_ => JsonResponse(new Dictionary<string, object>
        {
            ["history"] = history
        }));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("некорректный курсор"));
    }

    [Test]
    public async Task GetLqdtPriceHistoryAsync_reads_all_1075_rows_using_cursor_page_size_and_board_migration()
    {
        var handler = CreateFullHistoryHandler();
        var result = await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None);
        var priceRequests = handler.Requests;

        Assert.Multiple(() =>
        {
            Assert.That(priceRequests, Has.Count.EqualTo(11));
            Assert.That(priceRequests.Select(GetStartFromUrl),
                Is.EqualTo(Enumerable.Range(0, 11).Select(page => page * LqdtHistoryPageSize)));
            Assert.That(priceRequests.All(request => Uri.UnescapeDataString(request).Contains(
                "history.columns=TRADEDATE,SECID,BOARDID,LEGALCLOSEPRICE", StringComparison.Ordinal)), Is.True);
            Assert.That(result, Has.Count.EqualTo(LiveHistoryRows));
            Assert.That(result[0].TradeDate, Is.EqualTo(FirstCalculationDate));
            Assert.That(result[0].Close, Is.EqualTo(2m));
            Assert.That(result[^1].Close, Is.EqualTo(3.074m));
            Assert.That(result.Select(value => value.TradeDate).Distinct().Count(), Is.EqualTo(LiveHistoryRows));
        });
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_a_total_that_changes_between_pages()
    {
        var handler = new TestHttpMessageHandler(request =>
        {
            var start = GetStart(request.RequestUri!);
            return start == 0
                ? JsonResponse(HistoryPayload(GenerateLqdtRows(0, 100), 0, 101, 100))
                : JsonResponse(HistoryPayload(GenerateLqdtRows(start, 2), 100, 102, 2));
        });

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("изменил общий размер истории"));
    }

    [Test]
    public void GetLqdtPriceHistoryAsync_rejects_multiple_boards_for_the_same_trade_date()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(HistoryPayload(
        [
            ["2026-06-22", "LQDT", "TQTF", 2.0196m, 2.0196m],
            ["2026-06-22", "LQDT", "TQBR", 2.0197m, 2.0197m]
        ], 0, 2, 2)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateClient(handler).GetLqdtPriceHistoryAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("несколько цен LQDT за одну дату"));
    }

    [Test, Explicit("Проверяет фактические официальные цены закрытия MOEX; требует доступа в интернет.")]
    public async Task GetLqdtPriceHistoryAsync_returns_verified_live_history_and_exact_period_return()
    {
        var history = await CreateLiveClient().GetLqdtPriceHistoryAsync(CancellationToken.None);
        var from = new DateOnly(2026, 9, 9);
        var to = new DateOnly(2026, 10, 9);
        var result = new LqdtBenchmarkSeriesCalculator().Calculate(from, to, history);

        Assert.Multiple(() =>
        {
            Assert.That(history[0].TradeDate, Is.EqualTo(FirstCalculationDate));
            Assert.That(history.All(value => value.Close is > 0), Is.True);
            Assert.That(history.Select(value => value.TradeDate).Distinct().Count(), Is.EqualTo(history.Count));
            Assert.That(history.Single(value => value.TradeDate == from).Close, Is.EqualTo(2.0811m));
            Assert.That(history.Single(value => value.TradeDate == new DateOnly(2026, 9, 11)).Close, Is.EqualTo(2.0840m));
            Assert.That(history.Single(value => value.TradeDate == to).Close, Is.EqualTo(2.1063m));
            Assert.That(result.Series.ProfitPercent, Is.EqualTo(0.012108980827447).Within(0.000000000001));
            Assert.That(result.StartPriceDate, Is.EqualTo(from));
            Assert.That(result.EndPriceDate, Is.EqualTo(to));
        });
    }

    private static MoexHttpClient CreateClient(TestHttpMessageHandler handler)
    {
        var issClient = new MoexIssClient(
            new FixedHttpClientFactory(new HttpClient(handler)),
            new MemoryCache(new MemoryCacheOptions()));
        return new MoexHttpClient(issClient);
    }

    private static MoexHttpClient CreateLiveClient()
    {
        var issClient = new MoexIssClient(new FixedHttpClientFactory(new HttpClient()),
            new MemoryCache(new MemoryCacheOptions()));
        return new MoexHttpClient(issClient);
    }

    private static TestHttpMessageHandler CreateTwoPageLqdtHistoryHandler() =>
        new(request =>
        {
            var start = GetStart(request.RequestUri!);
            var date = start == 0 ? "2022-07-22" : "2022-07-25";
            var close = start == 0 ? 9.99m : 10.00m;
            var legalClose = start == 0 ? 10.00m : 10.01m;
            return JsonResponse(HistoryPayload(
                [[date, "LQDT", start == 0 ? "TQTF" : "TQTF", close, legalClose]], start, 2, 1));
        });

    private static TestHttpMessageHandler CreateFullHistoryHandler() =>
        new(request =>
        {
            var start = GetStart(request.RequestUri!);
            var count = Math.Min(LqdtHistoryPageSize, LiveHistoryRows - start);
            return JsonResponse(HistoryPayload(GenerateLqdtRows(start, count), start, LiveHistoryRows, count));
        });

    private static object?[][] GenerateLqdtRows(int start, int count) =>
        Enumerable.Range(start, count)
            .Select(index =>
            {
                var board = index < 995 ? "TQTF" : "TQBR";
                var legalClose = 2m + index / 1000m;
                var close = legalClose - 0.0001m;
                return new object?[]
                {
                    GetTradingDate(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "LQDT", board, close, legalClose
                };
            })
            .ToArray();

    private static DateOnly GetTradingDate(int index)
    {
        var date = FirstCalculationDate;
        var tradingDays = 0;
        while (tradingDays < index)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                tradingDays++;
        }

        return date;
    }

    private static object HistoryPayload(
        object?[][] rows,
        int index,
        int total,
        int pageSize,
        string[]? columns = null) => new Dictionary<string, object>
    {
        ["history"] = new
        {
            columns = columns ?? ["TRADEDATE", "SECID", "BOARDID", "CLOSE", "LEGALCLOSEPRICE"],
            data = rows
        },
        ["history.cursor"] = new
        {
            columns = new[] { "INDEX", "TOTAL", "PAGESIZE" },
            data = new[] { new[] { index, total, pageSize } }
        }
    };

    private static int GetStart(Uri uri) => GetStartFromUrl(uri.ToString());

    private static int GetStartFromUrl(string url)
    {
        var query = new Uri(url).Query;
        var parameter = query.Split('&')
            .Select(part => part.TrimStart('?'))
            .Single(part => part.StartsWith("start=", StringComparison.Ordinal));
        return int.Parse(parameter["start=".Length..], CultureInfo.InvariantCulture);
    }

    private static HttpResponseMessage JsonResponse(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value))
    };

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> createResponse)
        : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(createResponse(request));
        }
    }
}
