using NUnit.Framework;
using Popo.Api.Models;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
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

    [Test]
    public void PositionAnnualizedBreakdown_UsesCamelCaseContract()
    {
        var lot = new PositionAnnualizedLotBreakdown(
            new DateOnly(2026, 10, 1), 2, 1_000, 10, 1, 2_022, 5, 10_110);
        var breakdown = new PositionAnnualizedReturnBreakdown(
            new DateOnly(2026, 10, 6), 20, 10_110, 72.2057, [lot]);
        var response = new PositionsPageResponse(
            [new PortfolioPositionRecord(
                "RU000A", "TQCB", "RUB", 2, 2, 0, 2_000, 2_022, 0, 1_000,
                ApproximateAnnualizedTotalPnlPercent: breakdown.AnnualizedPercent,
                ApproximateAnnualizedReturnBreakdown: breakdown)],
            new PositionTotalsResponse(2_040, 2_022, 18, 0.89));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var actual = json.RootElement.GetProperty("positions")[0].GetProperty("approximateAnnualizedReturnBreakdown");

        Assert.Multiple(() =>
        {
            Assert.That(actual.GetProperty("annualizedPercent").GetDouble(), Is.EqualTo(72.2057));
            Assert.That(actual.GetProperty("investedCapitalDays").GetDouble(), Is.EqualTo(10_110));
            Assert.That(actual.GetProperty("lots")[0].GetProperty("paidAccruedInterestPerUnit").GetDouble(), Is.EqualTo(10));
            Assert.That(actual.GetProperty("lots")[0].GetProperty("holdingDays").GetInt32(), Is.EqualTo(5));
        });
    }
}
