using System.Net;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using Popo.HttpClients;

namespace Popo.Tests;

[TestFixture]
public sealed class MoexLqdtIndexHistoryTests
{
    private static readonly DateOnly FirstCalculationDate = new(2022, 7, 22);
    private static readonly DateOnly FirstLqdtTradingDate = new(2022, 7, 22);

    [Test]
    public async Task GetLqdtIndexHistoryAsync_requests_paginated_lqdt_prices_and_lqdtm_index_history()
    {
        var handler = new LqdtHistoryHandler();
        using var httpClient = new HttpClient(handler);
        var issClient = new MoexIssClient(
            new FixedHttpClientFactory(httpClient),
            new MemoryCache(new MemoryCacheOptions()));
        var client = new MoexHttpClient(issClient);

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
            Assert.That(handler.Requests[2], Does.Contain("/engines/stock/markets/shares/securities/LQDT/candles.json"));
            Assert.That(handler.Requests[2], Does.Contain("interval=24"));
            Assert.That(handler.Requests[2], Does.Contain("from=2022-07-22"));
            Assert.That(handler.Requests[2], Does.Contain("start=0"));
            Assert.That(handler.Requests[3], Does.Contain("start=1"));
            Assert.That(lqdtmIndex.Select(x => x.TradeDate), Is.EqualTo(new DateOnly?[]
            {
                new(2022, 7, 22),
                new(2022, 7, 25)
            }));
            Assert.That(lqdtmIndex.Select(x => x.Close), Is.EqualTo(new decimal?[] { 1.0000m, 1.0001m }));
            Assert.That(lqdtPrices.Select(x => x.TradeDate), Is.EqualTo(new DateOnly?[]
            {
                new(2022, 7, 22),
                new(2022, 7, 25)
            }));
            Assert.That(lqdtPrices.Select(x => x.Close), Is.EqualTo(new decimal?[] { 10m, 10.01m }));
        });
    }

    [Test]
    public async Task GetLqdtIndexHistoryAsync_paginates_full_candle_pages_without_cursor_blocks()
    {
        var handler = new LqdtHistoryWithoutCursorHandler();
        using var httpClient = new HttpClient(handler);
        var issClient = new MoexIssClient(
            new FixedHttpClientFactory(httpClient),
            new MemoryCache(new MemoryCacheOptions()));

        var result = await new MoexHttpClient(issClient).GetLqdtIndexHistoryAsync(CancellationToken.None);
        var lqdtPrices = result.Where(value => value.SecId == "LQDT").ToArray();
        var lqdtmIndex = result.Where(value => value.SecId == "LQDTM").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(4));
            Assert.That(handler.Requests[0], Does.Contain("start=0"));
            Assert.That(handler.Requests[1], Does.Contain("start=100"));
            Assert.That(handler.Requests[2], Does.Contain("start=0"));
            Assert.That(handler.Requests[3], Does.Contain("start=500"));
            Assert.That(lqdtmIndex, Has.Length.EqualTo(101));
            Assert.That(lqdtPrices, Has.Length.EqualTo(501));
            Assert.That(lqdtPrices[0].TradeDate, Is.EqualTo(FirstLqdtTradingDate));
            Assert.That(lqdtPrices[^1].TradeDate, Is.EqualTo(FirstLqdtTradingDate.AddDays(500)));
        });
    }

    [Test, Explicit("Проверяет реальную публичную историю MOEX; требует доступа в интернет.")]
    public async Task GetLqdtIndexHistoryAsync_returns_live_values_from_moex_since_2022()
    {
        using var httpClient = new HttpClient();
        var issClient = new MoexIssClient(
            new FixedHttpClientFactory(httpClient),
            new MemoryCache(new MemoryCacheOptions()));
        var client = new MoexHttpClient(issClient);

        var result = await client.GetLqdtIndexHistoryAsync(CancellationToken.None);
        var lqdtPrices = result.Where(value => value.SecId == "LQDT").ToArray();
        var lqdtmIndex = result.Where(value => value.SecId == "LQDTM").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Empty);
            Assert.That(lqdtmIndex[0].TradeDate, Is.EqualTo(FirstCalculationDate));
            Assert.That(lqdtmIndex.All(x => x.TradeDate >= FirstCalculationDate), Is.True);
            Assert.That(lqdtPrices[0].TradeDate, Is.EqualTo(FirstLqdtTradingDate));
            Assert.That(lqdtPrices.All(x => x.TradeDate >= FirstLqdtTradingDate), Is.True);
            Assert.That(result.All(x => x.Close is > 0), Is.True);
        });
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class LqdtHistoryHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri!;
            Requests.Add(requestUri.ToString());
            var isCandleRequest = requestUri.AbsolutePath.Contains("/candles", StringComparison.Ordinal);
            var secondPage = requestUri.Query.Contains("start=1", StringComparison.Ordinal);
            var date = isCandleRequest
                ? secondPage ? "2022-07-25 00:00:00" : "2022-07-22 00:00:00"
                : secondPage ? "2022-07-25" : "2022-07-22";
            var close = isCandleRequest
                ? secondPage ? "10.01" : "10.00"
                : secondPage ? "1.0001" : "1.0000";
            var cursorIndex = secondPage ? 1 : 0;
            var collection = isCandleRequest ? "candles" : "history";
            var cursorCollection = isCandleRequest ? "candles.cursor" : "history.cursor";
            var columns = isCandleRequest ? "[\"begin\",\"close\"]" : "[\"TRADEDATE\",\"SECID\",\"CLOSE\"]";
            var row = isCandleRequest
                ? $$"""["{{date}}",{{close}}]"""
                : $$"""["{{date}}","LQDTM",{{close}}]""";
            var json = $$"""
                {
                  "{{collection}}": {
                    "columns": {{columns}},
                    "data": [{{row}}],
                    "metadata": {}
                  },
                  "{{cursorCollection}}": {
                    "columns": ["INDEX", "TOTAL", "PAGESIZE"],
                    "data": [[{{cursorIndex}}, 2, 1]],
                    "metadata": {}
                  }
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed class LqdtHistoryWithoutCursorHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri!;
            Requests.Add(requestUri.ToString());
            var isCandleRequest = requestUri.AbsolutePath.Contains("/candles", StringComparison.Ordinal);
            var startParameter = requestUri.Query
                .Split('&')
                .Select(part => part.TrimStart('?'))
                .Single(part => part.StartsWith("start=", StringComparison.Ordinal));
            var start = int.Parse(startParameter["start=".Length..]);
            var firstPageSize = isCandleRequest ? 500 : 100;
            var count = start switch
            {
                0 => firstPageSize,
                _ when start == firstPageSize => 1,
                _ => 0
            };
            var rows = Enumerable.Range(0, count)
                .Select(index =>
                {
                    var date = isCandleRequest
                        ? FirstLqdtTradingDate.AddDays(start + index)
                        : FirstCalculationDate.AddDays(start + index);
                    var dateValue = isCandleRequest
                        ? date.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return isCandleRequest
                        ? new object[] { dateValue, 10m + index }
                        : [dateValue, "LQDTM", 1m + index / 10_000m];
                })
                .ToArray();
            var json = isCandleRequest
                ? JsonSerializer.Serialize(new { candles = new { columns = new[] { "begin", "close" }, data = rows } })
                : JsonSerializer.Serialize(new { history = new { columns = new[] { "TRADEDATE", "SECID", "CLOSE" }, data = rows } });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}
