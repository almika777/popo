using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Storage;

namespace Popo.Tests;

public sealed class PopoSchemaTests
{
    [Test]
    public void Model_contains_required_market_rating_return_and_portfolio_ledger_tables()
    {
        using var context = CreateContext();
        var tables = context.Model.GetEntityTypes().Select(x => x.GetTableName())
            .Where(x => x is not null).ToHashSet(StringComparer.Ordinal);
        Assert.That(tables, Is.EquivalentTo(new[]
        {
            "ActiveSecEntities", "MoexBonds", "MoexBondSecurities", "MoexHistoryYieldsEntities",
            "MoexCouponsEntities", "MoexAmortsEntities", "MoexEmitents", "BondRatings",
            "CurrencyRates", "PortfolioValuations", "PortfolioCashFlows", "PortfolioTrades", "CashSnapshots",
            "MoneyMarketFunds", "MoneyMarketFundOperations", "InvestmentStrategySettings",
            "PositionRecommendationStates", "DailyVolumeStatistics", "InitializationJobStates"
        }));
    }

    [Test]
    public void Currency_rates_have_date_and_currency_composite_primary_key()
    {
        using var context = CreateContext();
        var entity = context.Model.GetEntityTypes().Single(x => x.ClrType.Name == "CurrencyRateEntity");
        Assert.That(entity.FindPrimaryKey()!.Properties.Select(x => x.Name),
            Is.EqualTo(new[] { "RateDate", "CurrencyCode" }));
    }

    [Test]
    public void Investment_strategy_settings_store_nominal_and_trading_currency_filters()
    {
        using var context = CreateContext();
        var entity = context.Model.GetEntityTypes()
            .Single(x => x.ClrType.Name == "InvestmentStrategySettingsEntity");

        Assert.Multiple(() =>
        {
            Assert.That(entity.FindProperty("FaceUnit"), Is.Not.Null);
            Assert.That(entity.FindProperty("CurrencyId"), Is.Not.Null);
        });
    }

    [TestCase("Popo.Storage.Entities.Moex.MoexBondSecurityEntity", "SecId", "BoardId")]
    [TestCase("Popo.Storage.Entities.Moex.MoexHistoryYieldsEntity", "TradeDate", "SecId", "BoardId")]
    public void Composite_market_entities_have_expected_primary_key(string entityName, params string[] expected)
    {
        using var context = CreateContext();
        var entity = context.Model.GetEntityTypes().Single(x => x.ClrType.FullName == entityName);
        Assert.That(entity.FindPrimaryKey()!.Properties.Select(x => x.Name), Is.EqualTo(expected));
    }

    private static PopoDbContext CreateContext() => new(new DbContextOptionsBuilder<PopoDbContext>()
        .UseNpgsql("Host=localhost;Database=popo").Options);
}
