using System.Net;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using Popo.HttpClients;
using Popo.HttpClients.Contracts.Iss;

namespace Popo.Tests;

[TestFixture]
public sealed class MoexMoneyMarketFundMarketdataTests
{
    [Test]
    public async Task FreshBondMarketdataIsNotCached()
    {
        var handler = new FixedResponseHandler(HttpStatusCode.OK, """
            {
              "marketdata": {
                "columns": ["SECID", "BOARDID"],
                "data": [],
                "metadata": {}
              }
            }
            """);
        using var httpClient = new HttpClient(handler);
        var client = new MoexIssClient(new FixedHttpClientFactory(httpClient), new MemoryCache(new MemoryCacheOptions()));

        var first = await client.GetActiveBondsMarketdataFreshAsync(CancellationToken.None);
        var firstRequestCount = handler.RequestCount;
        var second = await client.GetActiveBondsMarketdataFreshAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(firstRequestCount, Is.GreaterThan(0));
            Assert.That(handler.RequestCount, Is.EqualTo(firstRequestCount * 2));
        });
    }

    [Test]
    public async Task EmptySuccessfulMarketdataResponseReturnsAnUnpricedFundQuote()
    {
        var handler = new FixedResponseHandler(HttpStatusCode.OK, """
            {
              "marketdata": {
                "columns": ["SECID", "BOARDID", "LAST", "LCURRENTPRICE", "MARKETPRICE2", "MARKETPRICE", "LCLOSEPRICE", "CLOSEPRICE", "SYSTIME"],
                "data": [],
                "metadata": {}
              }
            }
            """);
        using var httpClient = new HttpClient(handler);
        var client = new MoexIssClient(new FixedHttpClientFactory(httpClient), new MemoryCache(new MemoryCacheOptions()));

        var result = await client.GetMoneyMarketFundMarketdataAsync("LQDT", "TQBR", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.SecId, Is.EqualTo("LQDT"));
            Assert.That(result.BoardId, Is.EqualTo("TQBR"));
            Assert.That(result.CurrentPrice, Is.Null);
        });
    }

    [Test]
    public void HttpFailureIsNotConvertedToAnUnpricedQuote()
    {
        var handler = new FixedResponseHandler(HttpStatusCode.ServiceUnavailable, "{}");
        using var httpClient = new HttpClient(handler);
        var client = new MoexIssClient(new FixedHttpClientFactory(httpClient), new MemoryCache(new MemoryCacheOptions()));

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetMoneyMarketFundMarketdataAsync("LQDT", "TQBR", CancellationToken.None));
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        private int _requestCount;
        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
        }
    }
}
