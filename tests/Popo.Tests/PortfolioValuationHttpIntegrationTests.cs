using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Popo.Tests;

[Explicit("Требует запущенный Popo.Api на localhost:5109 и использует локальную БД")]
public sealed class PortfolioValuationHttpIntegrationTests
{
    [Test]
    public async Task PutValuationUpdatesTheSameRecordOverHttp()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("POPO_API_URL") ?? "http://localhost:5109/api/")
        };

        var rows = await client.GetFromJsonAsync<List<ValuationDto>>("portfolio/valuations");
        Assert.That(rows, Is.Not.Null.And.Not.Empty);
        var row = rows![0];
        var originalValue = row.TotalValue;
        var updatedValue = originalValue + 1;

        try
        {
            using var options = new HttpRequestMessage(HttpMethod.Options, $"portfolio/valuations/{row.Id}");
            options.Headers.TryAddWithoutValidation("Origin", "http://localhost:3000");
            options.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "PUT");
            options.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "content-type");
            var optionsResponse = await client.SendAsync(options);
            Assert.That(optionsResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(optionsResponse.Headers.GetValues("Access-Control-Allow-Origin"), Does.Contain("http://localhost:3000"));

            using var put = new HttpRequestMessage(HttpMethod.Put, $"portfolio/valuations/{row.Id}")
            {
                Content = JsonContent.Create(new { date = row.Date, totalValue = updatedValue, comment = row.Comment })
            };
            put.Headers.TryAddWithoutValidation("Origin", "http://localhost:3000");
            var putResponse = await client.SendAsync(put);
            Assert.That(putResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            var updatedRows = await client.GetFromJsonAsync<List<ValuationDto>>("portfolio/valuations");
            var updatedRow = updatedRows!.Single(x => x.Id == row.Id);
            Assert.That(updatedRow.TotalValue, Is.EqualTo(updatedValue));
        }
        finally
        {
            await client.PutAsJsonAsync($"portfolio/valuations/{row.Id}", new
            {
                date = row.Date,
                totalValue = originalValue,
                comment = row.Comment
            });
        }
    }

    [Test]
    public async Task PutMoneyMarketFundReturnsThePersistedValuesOverHttp()
    {
        using var client = CreateClient();
        var rows = await client.GetFromJsonAsync<List<MoneyMarketFundDto>>("cash/money-market-funds");
        Assert.That(rows, Is.Not.Null.And.Not.Empty);

        var row = rows![0];
        var originalQuantity = row.Quantity;
        var originalAveragePrice = row.AveragePrice;
        var updatedQuantity = originalQuantity + 1;
        var updatedAveragePrice = originalAveragePrice + 0.01;

        try
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"cash/money-market-funds/{row.Id}")
            {
                Content = JsonContent.Create(new { secId = row.SecId, quantity = updatedQuantity, averagePrice = updatedAveragePrice })
            };
            var putResponse = await client.SendAsync(put);

            Assert.That(putResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var updatedResponse = await putResponse.Content.ReadFromJsonAsync<MoneyMarketFundDto>();
            Assert.That(updatedResponse, Is.Not.Null);
            Assert.That(updatedResponse!.Id, Is.EqualTo(row.Id));
            Assert.That(updatedResponse.Quantity, Is.EqualTo(updatedQuantity));
            Assert.That(updatedResponse.AveragePrice, Is.EqualTo(updatedAveragePrice));
        }
        finally
        {
            await client.PutAsJsonAsync($"cash/money-market-funds/{row.Id}", new
            {
                secId = row.SecId,
                quantity = originalQuantity,
                averagePrice = originalAveragePrice
            });
        }
    }

    [Test]
    public async Task PutCashSnapshotReturnsThePersistedValuesOverHttp()
    {
        using var client = CreateClient();
        var rows = await client.GetFromJsonAsync<List<CashSnapshotDto>>("cash/snapshots");
        Assert.That(rows, Is.Not.Null.And.Not.Empty);

        var row = rows![0];
        var updatedAmount = row.Amount + 1;
        var updatedComment = $"{row.Comment} updated";

        try
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"cash/snapshots/{row.Id}")
            {
                Content = JsonContent.Create(new
                {
                    currencyId = row.CurrencyId,
                    snapshotDate = row.SnapshotDate,
                    amount = updatedAmount,
                    comment = updatedComment
                })
            };
            var putResponse = await client.SendAsync(put);

            Assert.That(putResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var updatedResponse = await putResponse.Content.ReadFromJsonAsync<CashSnapshotDto>();
            Assert.That(updatedResponse, Is.Not.Null);
            Assert.That(updatedResponse!.Id, Is.EqualTo(row.Id));
            Assert.That(updatedResponse.Amount, Is.EqualTo(updatedAmount));
            Assert.That(updatedResponse.Comment, Is.EqualTo(updatedComment.Trim()));
        }
        finally
        {
            await client.PutAsJsonAsync($"cash/snapshots/{row.Id}", new
            {
                currencyId = row.CurrencyId,
                snapshotDate = row.SnapshotDate,
                amount = row.Amount,
                comment = row.Comment
            });
        }
    }

    private static HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("POPO_API_URL") ?? "http://localhost:5109/api/")
    };

    private sealed record ValuationDto(Guid Id, string Date, double TotalValue, string Comment);
    private sealed record MoneyMarketFundDto(Guid Id, string SecId, string BoardId, double Quantity, double AveragePrice);
    private sealed record CashSnapshotDto(Guid Id, string CurrencyId, string SnapshotDate, double Amount, string Comment);
}
