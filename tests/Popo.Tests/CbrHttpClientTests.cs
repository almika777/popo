using System.Net;
using System.Net.Http;
using NUnit.Framework;
using Popo.Core.Contracts;
using Popo.HttpClients;

namespace Popo.Tests;

public sealed class CbrHttpClientTests
{
    [Test]
    public async Task GetCurrenciesAsync_maps_daily_currency_definitions()
    {
        using var client = CreateClient("""
            <?xml version="1.0" encoding="UTF-8"?>
            <ValCurs Date="06.08.2026" name="Foreign Currency Market">
              <Valute ID="R01235"><NumCode>840</NumCode><CharCode>USD</CharCode><Nominal>1</Nominal><Name>Доллар США</Name><Value>78,0000</Value></Valute>
            </ValCurs>
            """);

        var result = await new CbrHttpClient(client).GetCurrenciesAsync(CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(new CbrCurrencyDefinitionDto(
            "R01235", "USD", "Доллар США")));
    }

    [Test]
    public async Task GetCurrencyRatesAsync_maps_dynamic_rate_and_unit_rate()
    {
        using var client = CreateClient("""
            <?xml version="1.0" encoding="UTF-8"?>
            <ValCurs ID="R01235" DateRange1="01.01.2025" DateRange2="02.01.2025" name="Доллар США">
              <Record Date="02.01.2025" Id="R01235"><Nominal>1</Nominal><Value>89,0000</Value><VunitRate>89,0000</VunitRate></Record>
            </ValCurs>
            """);
        var definition = new CbrCurrencyDefinitionDto("R01235", "USD", "Доллар США");

        var result = await new CbrHttpClient(client).GetCurrencyRatesAsync(
            definition,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 2),
            CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].RateDate, Is.EqualTo(new DateOnly(2025, 1, 2)));
        Assert.That(result[0].UnitRate, Is.EqualTo(89d));
    }

    private static HttpClient CreateClient(string content) =>
        new(new StubHandler(content)) { BaseAddress = new Uri("https://www.cbr.ru/") };

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
    }
}
