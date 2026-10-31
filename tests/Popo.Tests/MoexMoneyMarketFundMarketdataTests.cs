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
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
    }
}
