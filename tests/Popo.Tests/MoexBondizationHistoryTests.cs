using System.Net;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using Popo.HttpClients;
using Popo.HttpClients.Contracts.Iss;

namespace Popo.Tests;

[TestFixture]
public sealed class MoexBondizationHistoryTests
{
    [Test]
    public async Task GetFutureAmortsAndCoupons_LoadsEveryPageAndMapsRecordDate()
    {
        var handler = new BondizationHandler();
        using var httpClient = new HttpClient(handler);
        var client = new MoexIssClient(new FixedHttpClientFactory(httpClient), new MemoryCache(new MemoryCacheOptions()));

        var result = await client.GetFutureAmortsAndCoupons("RU000A107738", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests.All(x => !x.Contains("from=")), Is.True);
            Assert.That(result.Coupons, Has.Count.EqualTo(2));
            Assert.That(result.Coupons[0].RecordDate, Is.EqualTo(new DateOnly(2026, 6, 14)));
            Assert.That(result.Coupons[0].ValueInRub, Is.EqualTo(2_245.19));
            Assert.That(result.Coupons[1].CouponDate, Is.EqualTo(new DateOnly(2026, 9, 15)));
        });
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class BondizationHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            var secondPage = request.RequestUri.Query.Contains("start=100", StringComparison.Ordinal);
            var couponDate = secondPage ? "2026-09-15" : "2026-06-15";
            var recordDate = secondPage ? "2026-09-14" : "2026-06-14";
            var couponValue = secondPage ? "2009.73" : "2245.19";
            var json = $$"""
                {
                  "coupons": {
                    "columns": ["isin", "secid", "coupondate", "startdate", "recorddate", "initialfacevalue", "facevalue", "faceunit", "value", "valueprc", "value_rub", "primary_boardid"],
                    "data": [["RU000A107738", "TCSG", "{{couponDate}}", "2026-03-15", "{{recordDate}}", 1000, 1000, "USD", 25, 10, {{couponValue}}, "TQCB"]],
                    "metadata": {}
                  },
                  "coupons.cursor": {
                    "columns": ["INDEX", "TOTAL", "PAGESIZE"],
                    "data": [[{{(secondPage ? 100 : 0)}}, 101, 100]],
                    "metadata": {}
                  },
                  "amortizations": {
                    "columns": ["isin", "secid", "amortdate", "initialfacevalue", "facevalue", "faceunit", "value", "valueprc", "value_rub", "primary_boardid"],
                    "data": [],
                    "metadata": {}
                  },
                  "amortizations.cursor": {
                    "columns": ["INDEX", "TOTAL", "PAGESIZE"],
                    "data": [[0, 0, 100]],
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
