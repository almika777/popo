using NUnit.Framework;
using Popo.Api.Services;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Trades;
using Popo.Core.PortfolioReturns;

namespace Popo.Tests;

public sealed class PortfolioServiceTests
{
    [Test]
    public async Task GetCashBalancesAsync_CombinesCashSnapshotsTradesFundOperationsAndFlows()
    {
        var snapshot = new CashSnapshotRecord(
            Guid.NewGuid(), "RUB", new DateOnly(2026, 2, 1), 100_000, "start");
        var trade = new PortfolioTradeRecord(
            Guid.NewGuid(), "RU000C", "TQCB", "RUB", new DateOnly(2026, 2, 2), TradeSide.Buy,
            10, 100, 1_000, 0, 0, 0, 1_000);
        var fundOperation = new MoneyMarketFundOperationRecord(
            Guid.NewGuid(), "LQDT", new DateOnly(2026, 2, 3), TradeSide.Buy, 20, 10, 2, 202);
        var tax = new PortfolioCashFlowRecord(
            Guid.NewGuid(), new DateOnly(2026, 2, 3), PortfolioCashFlowType.Tax, 10, "tax");
        var service = new PortfolioService(
            new FakeCashProvider([snapshot]),
            new FakeTradesProvider([trade]),
            new FakeMoneyMarketFundsProvider([fundOperation]),
            new FakeCashFlowsProvider([tax]));

        var balances = await service.GetCashBalancesAsync(new DateOnly(2026, 2, 3), CancellationToken.None);

        Assert.That(balances.Single().Amount, Is.EqualTo(98_788));
    }

    private sealed class FakeCashProvider(IReadOnlyList<CashSnapshotRecord> snapshots) : IPortfolioCashProvider
    {
        public Task<IReadOnlyList<CashSnapshotRecord>> GetCashSnapshotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshots);

        public Task<CashSnapshotRecord?> GetCashSnapshotAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CashSnapshotRecord> AddCashSnapshotAsync(CashSnapshot snapshot, string comment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CashSnapshotRecord?> UpdateCashSnapshotAsync(Guid id, CashSnapshot snapshot, string comment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteCashSnapshotAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTradesProvider(IReadOnlyList<PortfolioTradeRecord> trades) : IPortfolioTradesProvider
    {
        public Task<IReadOnlyList<PortfolioTradeRecord>> GetTradesAsync(CancellationToken cancellationToken) => Task.FromResult(trades);
        public Task<PortfolioTradeRecord?> GetTradeAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PortfolioTradeRecord> AddTradeAsync(PortfolioTrade trade, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UpdateTradeAsync(Guid id, PortfolioTrade trade, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteTradeAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMoneyMarketFundsProvider(
        IReadOnlyList<MoneyMarketFundOperationRecord> operations) : IPortfolioMoneyMarketFundsProvider
    {
        public Task<IReadOnlyList<MoneyMarketFundOperationRecord>> GetMoneyMarketFundOperationsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(operations);

        public Task<IReadOnlyList<MoneyMarketFundRecord>> GetMoneyMarketFundsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoneyMarketFundRecord> AddMoneyMarketFundAsync(MoneyMarketFund fund, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoneyMarketFundRecord?> UpdateMoneyMarketFundAsync(Guid id, MoneyMarketFund fund, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteMoneyMarketFundAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MoneyMarketFundOperationRecord> AddMoneyMarketFundOperationAsync(MoneyMarketFundOperation operation, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCashFlowsProvider(
        IReadOnlyList<PortfolioCashFlowRecord> cashFlows) : IPortfolioCashFlowsProvider
    {
        public Task<IReadOnlyList<PortfolioCashFlowRecord>> GetCashFlowsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(cashFlows);

        public Task<PortfolioCashFlowRecord?> GetCashFlowAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PortfolioCashFlowRecord> AddCashFlowAsync(DateOnly date, PortfolioCashFlowType type, double amount, string comment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UpdateCashFlowAsync(Guid id, DateOnly date, PortfolioCashFlowType type, double amount, string comment, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteCashFlowAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
