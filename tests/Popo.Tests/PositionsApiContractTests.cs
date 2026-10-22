using NUnit.Framework;
using Popo.Api.Models;
using Popo.Core.Portfolio;
using System.Text.Json;

namespace Popo.Tests;

public sealed class PositionsApiContractTests
{
    [Test]
    public void PageResponse_ContainsPositionsAndTotalsWithoutRecommendations()
    {
        var response = new PositionsPageResponse(
            [new PortfolioPositionRecord("RU000A", "TQCB", "RUB", 1, 1, 0, 1_000, 1_000, 0, 1_000)],
            new PositionTotalsResponse(1_100, 1_000, 100, 10));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Multiple(() =>
        {
            Assert.That(response.Positions, Has.Count.EqualTo(1));
            Assert.That(response.Totals.MarketValueRub, Is.EqualTo(1_100));
            Assert.That(json.RootElement.TryGetProperty("recommendation", out _), Is.False);
        });
    }
}
