using NUnit.Framework;
using Popo.Api.Services.BrokerReports;
using Popo.Core.Bonds;
using Popo.Core.Portfolio;

namespace Popo.Tests;

public sealed class BrokerReportFaceValueResolverTests
{
    [Test]
    public async Task ResolveAsync_UsesHistoricalFaceValueForEachTradeDate()
    {
        var bondsService = new HistoricalBondsService(500);
        var resolver = new BrokerReportFaceValueResolver(bondsService);
        var trades = new[]
        {
            new ParsedBrokerTrade(
                new DateOnly(2024, 6, 20), new TimeOnly(10, 30), "123456", "RU000A",
                TradeSide.Buy, 50, "%", 1, 500, 0, 500, "RUB", 0, 0, 0, "TQCB")
        };
        var bonds = new Dictionary<string, BondSearchResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["RU000A"] = new BondSearchResult("RU000A", "TQCB", "RUB", "Bond", 0, 1_000)
        };

        var result = await resolver.ResolveAsync(trades, bonds, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result[0], Is.EqualTo(500));
            Assert.That(bondsService.Requests.Single(), Is.EqualTo(
                new BondFaceValueRequest("RU000A", "TQCB", new DateOnly(2024, 6, 20))));
        });
    }

    private sealed class HistoricalBondsService(double? faceValue) : IBondsService
    {
        public List<BondFaceValueRequest> Requests { get; } = [];

        public Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
            IReadOnlyList<BondFaceValueRequest> requests,
            CancellationToken cancellationToken)
        {
            Requests.AddRange(requests);
            return Task.FromResult<IReadOnlyList<BondFaceValueResult>>(requests.Select(request =>
                new BondFaceValueResult(request.SecId, request.BoardId, request.TradeDate, faceValue)).ToArray());
        }

        public Task<IReadOnlyList<BondSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
