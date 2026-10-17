using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Popo.Api.Controllers;
using Popo.Api.Models;
using Popo.Core.Bonds;

namespace Popo.Tests;

public sealed class BondsControllerTests
{
    [Test]
    public async Task Search_TrimsQueryAndReturnsServiceResults()
    {
        var expected = new[] { new BondSearchResult("RU000A", "TQCB", "RUB", "Test bond", 0, 1_000) };
        var service = new RecordingBondsService(expected);
        var cancellationToken = new CancellationTokenSource().Token;
        var controller = new BondsController(service);

        var response = await controller.Search(new BondSearchRequest { Query = " RU000A " }, cancellationToken);

        Assert.That(service.Query, Is.EqualTo("RU000A"));
        Assert.That(service.CancellationToken, Is.EqualTo(cancellationToken));
        Assert.That(response.Result, Is.TypeOf<OkObjectResult>());
        Assert.That(((OkObjectResult)response.Result!).Value, Is.SameAs(expected));
    }

    [Test]
    public void SearchResult_SerializesUnknownAccruedInterestAsNull()
    {
        var result = new BondSearchResult("RU000A", "TQCB", "RUB", "Test bond", null, 1_000);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.That(
            document.RootElement.GetProperty("accruedInterest").ValueKind,
            Is.EqualTo(JsonValueKind.Null));
    }

    private sealed class RecordingBondsService(IReadOnlyList<BondSearchResult> searchResults) : IBondsService
    {
        public string? Query { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<BondSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            Query = query;
            CancellationToken = cancellationToken;
            return Task.FromResult(searchResults);
        }

        public Task<IReadOnlyList<BondFaceValueResult>> GetHistoricalFaceValuesAsync(
            IReadOnlyList<BondFaceValueRequest> requests,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BondFaceValueResult>>([]);

        public Task<IReadOnlyList<string>> GetTradingCurrenciesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetFaceUnitsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
