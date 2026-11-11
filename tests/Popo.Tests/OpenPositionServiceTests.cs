using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Popo.Api.Services.Position;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;

namespace Popo.Tests;

public sealed class OpenPositionServiceTests
{
    [Test]
    public async Task GetAsync_ExcludesClosedPositionsAndSerializesOpenPositions()
    {
        var date = new DateOnly(2026, 1, 1);
        var records = new[]
        {
            new PortfolioTradeRecord(Guid.NewGuid(), "CLOSED", "TQCB", "RUB", date, TradeSide.Buy, 2, 1000, 1000, 0, 0, 0, 2000),
            new PortfolioTradeRecord(Guid.NewGuid(), "CLOSED", "TQCB", "RUB", date.AddDays(1), TradeSide.Sell, 2, 1000, 1000, 0, 0, 0, 2000),
            new PortfolioTradeRecord(Guid.NewGuid(), "OPEN", "TQCB", "RUB", date, TradeSide.Buy, 1, 1000, 1000, 0, 0, 0, 1000)
        };
        var trades = Stub<IPortfolioTradesProvider>(_ => Task.FromResult<IReadOnlyList<PortfolioTradeRecord>>(records));
        var securities = new[] { "CLOSED", "OPEN" }.Select(id => new MoexBond
        {
            SecId = id, BoardId = "TQCB", ShortName = id, FaceValue = 1000, FaceUnit = "RUB",
            CouponValue = 90, CouponPeriod = 90, BondType = "Фикс с известным купоном"
        }).ToList();
        var quotes = securities.Select(bond => new MoexMarketdata { SecId = bond.SecId, BoardId = bond.BoardId, LCurrentPrice = 110 }).ToList();
        var moex = Stub<IMoexHttpClient>(method => method.Name switch
        {
            nameof(IMoexHttpClient.GetActiveBondsSecuritiesAsync) => Task.FromResult(new Dictionary<string, List<MoexBond>> { ["TQCB"] = securities }),
            nameof(IMoexHttpClient.GetActiveBondsMarketdataAsync) => Task.FromResult(new Dictionary<string, List<MoexMarketdata>> { ["TQCB"] = quotes }),
            _ => throw new NotSupportedException()
        });
        var rates = Stub<IPortfolioCurrencyRatesProvider>(_ => throw new NotSupportedException());
        var result = await new OpenPositionService(trades, moex, new PositionBuilderFactory(), rates).GetAsync(CancellationToken.None);

        Assert.DoesNotThrow(() => JsonSerializer.Serialize(result));
        Assert.That(result.Positions.Select(position => position.SecId), Is.EqualTo(new[] { "OPEN" }));
    }

    [Test]
    public async Task GetAsync_ReturnsMarketNameAndCalculatedTotals()
    {
        var trade = new PortfolioTradeRecord(Guid.NewGuid(), "BOND", "TQCB", "RUB", new DateOnly(2026, 1, 1),
            TradeSide.Buy, 2, 1_000, 1_000, 0, 0, 0, 2_000);
        var trades = Stub<IPortfolioTradesProvider>(method => method.Name == nameof(IPortfolioTradesProvider.GetTradesAsync)
            ? Task.FromResult<IReadOnlyList<PortfolioTradeRecord>>([trade]) : throw new NotSupportedException());
        var security = new MoexBond { SecId = "BOND", BoardId = "TQCB", ShortName = "Market name", FaceValue = 1000,
            FaceUnit = "RUB", CouponValue = 90, CouponPeriod = 90, BondType = "Фикс с известным купоном" };
        var quote = new MoexMarketdata { SecId = "BOND", BoardId = "TQCB", LCurrentPrice = 110 };
        var moex = Stub<IMoexHttpClient>(method => method.Name switch
        {
            nameof(IMoexHttpClient.GetActiveBondsSecuritiesAsync) => Task.FromResult(new Dictionary<string, List<MoexBond>> { ["TQCB"] = [security] }),
            nameof(IMoexHttpClient.GetActiveBondsMarketdataAsync) => Task.FromResult(new Dictionary<string, List<MoexMarketdata>> { ["TQCB"] = [quote] }),
            _ => throw new NotSupportedException()
        });
        var rates = Stub<IPortfolioCurrencyRatesProvider>(_ => throw new NotSupportedException());
        var result = await new OpenPositionService(trades, moex, new PositionBuilderFactory(), rates).GetAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Positions.Single().ShortName, Is.EqualTo("Market name"));
            Assert.That(result.Positions.Single().BondType, Is.EqualTo("Фикс с известным купоном"));
            Assert.That(result.Totals.MarketValueRub, Is.EqualTo(2_200));
            Assert.That(result.Totals.InvestedAmountRub, Is.EqualTo(2_000));
            Assert.That(result.Totals.UnrealizedPnlRub, Is.EqualTo(200));
            Assert.That(result.Totals.UnrealizedPnlPercent, Is.EqualTo(10));
        });
    }

    private static T Stub<T>(Func<MethodInfo, object> handler) where T : class
    {
        var instance = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)instance).Handler = handler;
        return instance;
    }

    public class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!);
    }
}
