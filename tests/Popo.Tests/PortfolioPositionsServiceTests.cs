using NUnit.Framework;
using Popo.Api.Services;
using Popo.Api.Services.Position;
using Popo.Core;
using Popo.Core.Common;
using Popo.Core.Contracts.Iss;
using Popo.Core.HttpClients;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;
using Popo.Core.Recommendations;

namespace Popo.Tests;

public sealed class PortfolioPositionsServiceTests
{
    [Test]
    public async Task FutureDatedTrade_IsExcludedFromPositionIncome()
    {
        const string secId = "FUTURE-TRADE";
        const string boardId = "TQCB";
        var asOf = MoscowTime.Today;
        var position = new PortfolioPositionRecord(secId, boardId, "RUB", 1, 1, 0, 900, 900, 0, 900);
        var futureTrade = new PortfolioTradeRecord(
            Guid.NewGuid(), secId, boardId, "RUB", asOf.AddDays(1), TradeSide.Buy,
            1, 900, 1_000, 0, 0, 0, 900);
        var moex = new FakeMoexHttpClient(
            new MoexBond
            {
                SecId = secId,
                BoardId = boardId,
                Isin = secId,
                InitialFaceValue = 1_000,
                FaceValue = 1_000,
                FaceUnit = "RUB",
                CouponPeriod = 182
            },
            new MoexMarketdata
            {
                SecId = secId,
                BoardId = boardId,
                LCurrentPrice = 99,
                SysTime = asOf.ToDateTime(TimeOnly.MinValue)
            });
        var service = new PortfolioPositionsService(
            new FakePortfolioService(),
            new FakeMoneyMarketFundsProvider(),
            new FakeTradesProvider([futureTrade]),
            new FakePositionsProvider(position),
            moex,
            null!,
            new FakeHistoryProvider(),
            new FakeCouponProvider(),
            null!);

        var result = (await service.GetPositionsAsync(CancellationToken.None)).Single();

        Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.Null);
    }

    [TestCase(1_000d)]
    [TestCase(0d)]
    public async Task IndexedNominalPosition_IncludesNominalGrowthInCleanPriceResult(double initialFaceValue)
    {
        const string secId = "RU000A10F504";
        const string boardId = "TQCB";
        const double expectedAverageBuyPrice = 1_032.2798786732458;
        const double expectedMarketValue = 1_917_944.4816;
        const double expectedCleanPriceResult = 35_065.9829;
        const double expectedApproximateTotalResult = 34_390.60854452;

        var tradeData = new (DateOnly Date, double Quantity, double Price, double FaceValue, double Commission)[]
        {
            (new DateOnly(2026, 7, 22), 489, 1026.24, 1026.65, 201.434544),
            (new DateOnly(2026, 7, 28), 196, 1028.9, 1029.11, 0),
            (new DateOnly(2026, 8, 7), 212, 1033.79, 1033.13, 88.053392),
            (new DateOnly(2026, 8, 11), 173, 1034.686, 1034.7, 71.9234352),
            (new DateOnly(2026, 8, 12), 629, 1035.0858, 1035.1, 261.61513928),
            (new DateOnly(2026, 8, 31), 125, 1041.1969, 1042.57, 52.347845)
        };
        var trades = tradeData.Select(x => new PortfolioTradeRecord(
            Guid.NewGuid(), secId, boardId, "RUB", x.Date, TradeSide.Buy, x.Quantity,
            x.Price, x.FaceValue, 0, x.Commission, 0, x.Quantity * x.Price + x.Commission)).ToArray();
        var portfolioService = new FakePortfolioService();
        var position = new PortfolioPositionRecord(
            secId,
            boardId,
            "RUB",
            1824,
            1824,
            0,
            1_882_878.4987,
            1_882_878.4987 + 675.37435548,
            0,
            expectedAverageBuyPrice);
        var moex = new FakeMoexHttpClient(
            new MoexBond
            {
                SecId = secId,
                BoardId = boardId,
                Isin = secId,
                ShortName = "ВЭБ2Р-58",
                InitialFaceValue = initialFaceValue,
                FaceValue = 1054.14,
                FaceUnit = "RUB",
                CouponValue = 0,
                CouponPeriod = 182
            },
            new MoexMarketdata
            {
                SecId = secId,
                BoardId = boardId,
                LCurrentPrice = 99.75,
                SysTime = new DateTime(2026, 9, 25)
            });
        var service = new PortfolioPositionsService(
            portfolioService,
            new FakeMoneyMarketFundsProvider(),
            new FakeTradesProvider(trades),
            new FakePositionsProvider(position),
            moex,
            null!,
            new FakeHistoryProvider(),
            new FakeCouponProvider(),
            null!);

        var result = (await service.GetPositionsAsync(CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPrice, Is.EqualTo(expectedAverageBuyPrice).Within(0.000001));
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(1053.9808114554098).Within(0.000001));
            Assert.That(result.IsNominalIndexed, Is.True);
            Assert.That(result.MarketValue, Is.EqualTo(expectedMarketValue).Within(0.001));
            Assert.That(result.UnrealizedPnl, Is.EqualTo(expectedCleanPriceResult).Within(0.001));
            Assert.That(result.ApproximateTotalPnl, Is.EqualTo(expectedApproximateTotalResult).Within(0.001));
        });
    }

    [TestCase(600d, true, 100d)]
    [TestCase(800d, false, 0d)]
    public async Task MissingInitialNominal_UsesOnlyOpenFifoLotsToDetectGrowth(
        double openLotFaceValue,
        bool isNominalIndexed,
        double expectedCleanPriceResult)
    {
        const string secId = "RU000A10F504";
        const string boardId = "TQCB";
        var trades = new[]
        {
            new PortfolioTradeRecord(Guid.NewGuid(), secId, boardId, "RUB", new DateOnly(2026, 7, 22),
                TradeSide.Buy, 1, 500, 500, 0, 0, 0, 500),
            new PortfolioTradeRecord(Guid.NewGuid(), secId, boardId, "RUB", new DateOnly(2026, 7, 23),
                TradeSide.Buy, 1, openLotFaceValue, openLotFaceValue, 0, 0, 0, openLotFaceValue),
            new PortfolioTradeRecord(Guid.NewGuid(), secId, boardId, "RUB", new DateOnly(2026, 7, 24),
                TradeSide.Sell, 1, 600, 600, 0, 0, 0, 600)
        };
        var portfolioService = new FakePortfolioService();
        var position = new PortfolioPositionRecord(secId, boardId, "RUB", 1, 2, 1,
            500 + openLotFaceValue, 500 + openLotFaceValue, 600,
            (500 + openLotFaceValue) / 2);
        var moex = new FakeMoexHttpClient(
            new MoexBond
            {
                SecId = secId,
                BoardId = boardId,
                Isin = secId,
                InitialFaceValue = 0,
                FaceValue = 700,
                FaceUnit = "RUB",
                CouponPeriod = 182
            },
            new MoexMarketdata
            {
                SecId = secId,
                BoardId = boardId,
                LCurrentPrice = 100,
                SysTime = new DateTime(2026, 9, 25)
            });
        var service = new PortfolioPositionsService(
            portfolioService,
            new FakeMoneyMarketFundsProvider(),
            new FakeTradesProvider(trades),
            new FakePositionsProvider(position),
            moex,
            null!,
            new FakeHistoryProvider(),
            new FakeCouponProvider(),
            null!);

        var result = (await service.GetPositionsAsync(CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPrice, Is.EqualTo(openLotFaceValue));
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(700));
            Assert.That(result.IsNominalIndexed, Is.EqualTo(isNominalIndexed));
            Assert.That(result.UnrealizedPnl, Is.EqualTo(expectedCleanPriceResult));
        });
    }

    [TestCase(1_000d)]
    [TestCase(0d)]
    public async Task AmortizedNominalPosition_AdjustsOpenCostFromTradeNominalToCurrentNominal(
        double initialFaceValue)
    {
        const string secId = "RU000A107UU5";
        const string boardId = "TQCB";
        const double expectedAverageBuyPrice = 751.5057692307693;
        const double expectedAverageBuyPriceAtCurrentFaceValue = 501.0038461538462;
        const double expectedMarketValue = 150_636.20;
        const double expectedCleanPriceResult = 836.05;
        const double expectedApproximateTotalResult = 745.182942;

        var tradeData = new (DateOnly Date, double Quantity, double Price, double Commission)[]
        {
            (new DateOnly(2026, 9, 18), 27, 749.475, 8.22953),
            (new DateOnly(2026, 9, 17), 28, 751.2, 8.53904),
            (new DateOnly(2026, 9, 17), 16, 751.425, 4.88472),
            (new DateOnly(2026, 9, 14), 28, 751.35, 8.52632),
            (new DateOnly(2026, 9, 9), 20, 750.9, 6.06936),
            (new DateOnly(2026, 9, 9), 30, 750.9, 9.104),
            (new DateOnly(2026, 9, 9), 15, 750.75, 4.55112),
            (new DateOnly(2026, 9, 4), 9, 752.325, 2.71957),
            (new DateOnly(2026, 9, 4), 55, 752.325, 16.69355),
            (new DateOnly(2026, 9, 4), 1, 752.4, 0.303548),
            (new DateOnly(2026, 9, 4), 70, 752.325, 21.2463)
        };
        var trades = tradeData.Select(x => new PortfolioTradeRecord(
            Guid.NewGuid(), secId, boardId, "RUB", x.Date, TradeSide.Buy, x.Quantity,
            x.Price, 750, 0, x.Commission, 0, x.Quantity * x.Price + x.Commission)).ToArray();
        var portfolioService = new FakePortfolioService();
        var position = new PortfolioPositionRecord(
            secId,
            boardId,
            "RUB",
            299,
            299,
            0,
            224_700.225,
            227_258.512058,
            0,
            expectedAverageBuyPrice);
        var moex = new FakeMoexHttpClient(
            new MoexBond
            {
                SecId = secId,
                BoardId = boardId,
                Isin = secId,
                ShortName = "Брус 2Р02",
                InitialFaceValue = initialFaceValue,
                FaceValue = 500,
                FaceUnit = "SUR",
                CouponValue = 0,
                CouponPeriod = 182
            },
            new MoexMarketdata
            {
                SecId = secId,
                BoardId = boardId,
                LCurrentPrice = 100.76,
                SysTime = new DateTime(2026, 9, 25)
            });
        var service = new PortfolioPositionsService(
            portfolioService,
            new FakeMoneyMarketFundsProvider(),
            new FakeTradesProvider(trades),
            new FakePositionsProvider(position),
            moex,
            null!,
            new FakeHistoryProvider(),
            new FakeCouponProvider(),
            null!);

        var result = (await service.GetPositionsAsync(CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.AverageBuyPrice, Is.EqualTo(expectedAverageBuyPrice).Within(0.000001));
            Assert.That(result.AverageBuyPriceAtCurrentFaceValue, Is.EqualTo(expectedAverageBuyPriceAtCurrentFaceValue).Within(0.000001));
            Assert.That(result.IsNominalIndexed, Is.False);
            Assert.That(result.MarketValue, Is.EqualTo(expectedMarketValue).Within(0.001));
            Assert.That(result.UnrealizedPnl, Is.EqualTo(expectedCleanPriceResult).Within(0.001));
            Assert.That(result.ApproximateTotalPnl, Is.EqualTo(expectedApproximateTotalResult).Within(0.001));
        });
    }

    private sealed class FakePortfolioService : IPortfolioService
    {
        public Task<IReadOnlyList<CashBalance>> GetCashBalancesAsync(DateOnly asOf, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakePositionsProvider(PortfolioPositionRecord position) : IPortfolioPositionsProvider
    {
        public Task<IReadOnlyList<PortfolioPositionRecord>> GetPositionsAsync(
            DateOnly asOf,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PortfolioPositionRecord>>([position]);
    }

    private sealed class FakeMoneyMarketFundsProvider : IPortfolioMoneyMarketFundsProvider
    {
        public Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(MoneyMarketFundOperation operation, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTradesProvider(IReadOnlyList<PortfolioTradeRecord> trades) : IPortfolioTradesProvider
    {
        public Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(trades);

        public Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMoexHttpClient(MoexBond security, MoexMarketdata quote) : IMoexHttpClient
    {
        public Task<Dictionary<string, List<MoexBond>>> GetActiveBondsSecuritiesAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexBond>> { [security.BoardId] = [security] });

        public Task<Dictionary<string, List<MoexMarketdata>>> GetActiveBondsMarketdataAsync(CancellationToken ct) =>
            Task.FromResult(new Dictionary<string, List<MoexMarketdata>> { [quote.BoardId] = [quote] });

        public Task<MoexBondization> GetFutureAmortsAndCoupons(string secId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexHistoryYields>> GetBondHistoryPageAsync(string secId, string boardId, DateOnly? from = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<string, List<MoexMarketdataYields>>> GetActiveBondsMarketdataYieldsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MoexMarketdata> GetMoneyMarketFundMarketdataAsync(string secId, string boardId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexTrade>> GetBondTradesAsync(string secId, string boardId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<SecDescription>> GetActiveSecAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MoexBondDescription> GetSecurityDescriptionAsync(string secId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MoexEmitentDescription>> GetAllEmitentDescriptionAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeHistoryProvider : IHistoryProvider
    {
        public Task<Dictionary<string, double>> GetMedianDailyVolume(CancellationToken ct) => Task.FromResult(new Dictionary<string, double>());
        public Task<Dictionary<string, DailyVolumeStatistics>> GetDailyVolumeStatistics(CancellationToken ct) => Task.FromResult(new Dictionary<string, DailyVolumeStatistics>());
    }

    private sealed class FakeCouponProvider : IPortfolioCouponProvider
    {
        public Task<IReadOnlyList<PublishedCouponValue>> GetLatestPublishedAsync(
            IReadOnlyCollection<PortfolioBondKey> bonds,
            DateOnly asOf,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishedCouponValue>>([]);
    }
}
