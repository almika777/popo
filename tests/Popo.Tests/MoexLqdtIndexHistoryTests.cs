using System.Net;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using Popo.HttpClients;

namespace Popo.Tests;

[TestFixture]
public sealed class MoexLqdtIndexHistoryTests
{
    private static readonly DateOnly FirstCalculationDate = new(2022, 7, 22);

    [Test]
    public async Task GetLqdtIndexHistoryAsync_requests_all_pages_since_first_calculation_and_maps_values()
    {
        var handler = new LqdtHistoryHandler();
        using var httpClient = new HttpClient(handler);
        var issClient = new MoexIssClient(
            new FixedHttpClientFactory(httpClient),
            new MemoryCache(new MemoryCacheOptions()));
        var client = new MoexHttpClient(issClient);

        var result = await client.GetLqdtIndexHistoryAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0], Does.Contain("/history/engines/stock/markets/index/securities/LQDTM.json"));
            Assert.That(handler.Requests[0], Does.Contain("from=2022-07-22"));
            Assert.That(handler.Requests[0], Does.Contain("start=0"));
            Assert.That(handler.Requests[1], Does.Contain("start=1"));
            Assert.That(result.Select(x => x.TradeDate), Is.EqualTo(new DateOnly?[]
            {
                new(2022, 7, 22),
                new(2022, 7, 25)
            }));
            Assert.That(result.Select(x => x.Close), Is.EqualTo(new decimal?[] { 1.0000m, 1.0001m }));
            Assert.That(result.All(x => x.SecId == "LQDTM"), Is.True);
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

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Empty);
            Assert.That(result[0].TradeDate, Is.EqualTo(FirstCalculationDate));
            Assert.That(result.All(x => x.TradeDate >= FirstCalculationDate), Is.True);
            Assert.That(result.All(x => x.SecId == "LQDTM"), Is.True);
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
            var secondPage = requestUri.Query.Contains("start=1", StringComparison.Ordinal);
            var date = secondPage ? "2022-07-25" : "2022-07-22";
            var close = secondPage ? "1.0001" : "1.0000";
            var cursorIndex = secondPage ? 1 : 0;
            var json = $$"""
                {
                  "history": {
                    "columns": ["TRADEDATE", "SECID", "CLOSE"],
                    "data": [["{{date}}", "LQDTM", {{close}}]],
                    "metadata": {}
                  },
                  "history.cursor": {
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
}
