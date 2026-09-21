using NUnit.Framework;
using Popo.Api.Models;
using Popo.Core.Portfolio;
using Popo.Core.Recommendations;

namespace Popo.Tests;

public sealed class PositionsApiContractTests
{
    [Test]
    public void PageResponse_ContainsPositionsTotalsAndRecommendation()
    {
        var response = new PositionsPageResponse(
            [new PortfolioPositionRecord("RU000A", "TQCB", "RUB", 1, 1, 0, 1_000, 1_000, 0, 1_000)],
            new PositionTotalsResponse(1_100, 1_000, 100, 10),
            new PositionRecommendationStateResponse(
                RecommendationStatus.Ready, null, null, null, null, []));

        Assert.Multiple(() =>
        {
            Assert.That(response.Positions, Has.Count.EqualTo(1));
            Assert.That(response.Totals.MarketValueRub, Is.EqualTo(1_100));
            Assert.That(response.Recommendation.Status, Is.EqualTo(RecommendationStatus.Ready));
        });
    }
}
