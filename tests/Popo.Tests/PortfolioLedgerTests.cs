using NUnit.Framework;
using Popo.Core.Contracts;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

public sealed class PortfolioLedgerTests
{
    [Test]
    public void MarketPrice_ConvertsNominalCurrencyToTradingCurrency()
    {
        var price = PortfolioMarketPriceCalculator.Calculate(1_000, "USD", "RUB", 99.5, 90, 1);

        Assert.That(price, Is.EqualTo(89_550).Within(0.001));
    }

    [Test]
    public void MarketPrice_UsesOneToOneRateWhenCurrenciesMatch()
    {
        var price = PortfolioMarketPriceCalculator.Calculate(1_000, "RUB", "RUB", 99.7, null, null);

        Assert.That(price, Is.EqualTo(997).Within(0.001));
    }

    [Test]
    public void MarketPrice_TreatsMoexSurAsRuble()
    {
        var price = PortfolioMarketPriceCalculator.Calculate(1_000, "SUR", "RUB", 99.7, null, null);

        Assert.That(price, Is.EqualTo(997).Within(0.001));
    }

    [Test]
    public void PositionWithUsdFaceUnitAndRubTradingCurrency_UsesCurrencyRateOnQuoteDate()
    {
        var quoteDate = DateOnly.FromDateTime(DateTime.Now);
        var security = new MoexBond
        {
            SecId = "RU000A107738",
            BoardId = "TQCB",
            FaceValue = 1_000,
            FaceUnit = "USD",
            CurrencyId = "RUB"
        };
        var quote = new MoexMarketdata
        {
            SecId = security.SecId,
            BoardId = security.BoardId,
            LCurrentPrice = 99.5,
            SysTime = quoteDate.ToDateTime(TimeOnly.MinValue)
        };
        var usdRate = new CbrCurrencyRateDto(quoteDate, "USD", "Доллар США", 1, 90, 90);
        var position = PortfolioPositionCalculator.ToRecord(
            PortfolioPositionCalculator.Calculate(
            [
                new PortfolioTrade(security.SecId, security.BoardId, security.CurrencyId, quoteDate,
                    TradeSide.Buy, 3, 90_900)
            ]).Single());

        var marketPrice = PortfolioMarketPriceCalculator.Calculate(
            security.FaceValue,
            security.FaceUnit,
            security.CurrencyId,
            quote.CurrentPrice,
            usdRate.UnitRate,
            1);
        var valuation = PortfolioPositionCalculator.Calculate(position, marketPrice);

        Assert.That(usdRate.RateDate, Is.EqualTo(quoteDate));
        Assert.That(marketPrice, Is.EqualTo(89_550).Within(0.001));
        Assert.That(valuation.MarketValue, Is.EqualTo(268_650).Within(0.001));
        Assert.That(valuation.UnrealizedPnl, Is.EqualTo(-4_050).Within(0.001));
        Assert.That(valuation.UnrealizedPnlPercent, Is.EqualTo(-1.4851485).Within(0.0001));
    }

    [Test]
    public void Cash_UsesAccruedInterestAndCommissionAmount()
    {
        var balances = PortfolioCashCalculator.Calculate(
            [new CashSnapshot("RUB", new DateOnly(2026, 1, 1), 10_000)],
            [
                new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 2), TradeSide.Buy, 10, 100, 1_000, 2, 1),
                new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 3), TradeSide.Sell, 2, 110, 1_000, 3, 1)
            ],
            new DateOnly(2026, 1, 3));

        Assert.That(balances.Single().Amount, Is.EqualTo(9_193.54).Within(0.000001));
    }

    [Test]
      public void Positions_AggregateBuysAndSellsBySecurityBoardAndCurrency()
      {
        var positions = PortfolioPositionCalculator.Calculate(
        [
            new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy, 100, 98),
            new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 2), TradeSide.Sell, 25, 99),
            new PortfolioTrade("RU000A", "TQOB", "RUB", new DateOnly(2026, 1, 2), TradeSide.Buy, 10, 100),
            new PortfolioTrade("RU000A", "TQCB", "CNY", new DateOnly(2026, 1, 2), TradeSide.Buy, 10, 100)
        ]);

        var position = positions.Single(x => x.BoardId == "TQCB" && x.CurrencyId == "RUB");

        Assert.That(position.Quantity, Is.EqualTo(75));
        Assert.That(position.BuyCleanAmount, Is.EqualTo(9800).Within(0.001));
        Assert.That(position.BuyAmount, Is.EqualTo(9800).Within(0.001));
        Assert.That(position.SellAmount, Is.EqualTo(2475).Within(0.001));
        Assert.That(PortfolioPositionCalculator.ToRecord(position).AverageBuyPrice, Is.EqualTo(98).Within(0.001));
         Assert.That(positions, Has.Exactly(3).Items);
      }

      [Test]
      public void PositionAverageBuyPrice_ExcludesAccruedInterestAndCommission()
      {
          var position = PortfolioPositionCalculator.Calculate(
          [
              new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 1), TradeSide.Buy, 2, 100, 1_000, 5, 10)
          ]).Single();

          var record = PortfolioPositionCalculator.ToRecord(position);

          Assert.That(record.AverageBuyPrice, Is.EqualTo(100).Within(0.001));
          Assert.That(record.BoughtAmount, Is.EqualTo(231).Within(0.001));
      }

      [Test]
      public void PositionValuation_CalculatesUnrealizedProfitFromCleanPrices()
      {
          var position = new PortfolioPositionRecord(
              "RU000A", "TQCB", "RUB", 10, 10, 0, 1_000, 1_050, 0, 100);

          var valuation = PortfolioPositionCalculator.Calculate(position, 110);

          Assert.That(valuation.MarketValue, Is.EqualTo(1_100).Within(0.001));
          Assert.That(valuation.UnrealizedPnl, Is.EqualTo(100).Within(0.001));
          Assert.That(valuation.UnrealizedPnlPercent, Is.EqualTo(10).Within(0.001));
      }

      [Test]
      public void PositionValuation_ReturnsNoDataWhenMarketPriceIsUnavailable()
      {
          var position = new PortfolioPositionRecord(
              "RU000A", "TQCB", "RUB", 10, 10, 0, 1_000, 1_000, 0, 100);

          var valuation = PortfolioPositionCalculator.Calculate(position, null);

          Assert.That(valuation.MarketValue, Is.Null);
          Assert.That(valuation.UnrealizedPnl, Is.Null);
      }

      [Test]
      public void PositionIncome_NormalizesPurchasePriceToCurrentNominalAfterAmortization()
      {
          var purchasePricePercent = PortfolioMarketPriceCalculator.CalculatePricePercent(
              500, 500, "RUB", "RUB", null, null);
          var purchasePriceAtCurrentNominal = PortfolioMarketPriceCalculator.Calculate(
              250, "RUB", "RUB", purchasePricePercent, null, null);

          var result = PortfolioPositionCalculator.Calculate(
              [
                  new PositionIncomeTrade(
                      new DateOnly(2026, 8, 1), TradeSide.Buy, 1,
                      purchasePriceAtCurrentNominal!.Value, 0, purchasePricePercent)
              ],
              new DateOnly(2026, 8, 28),
              250,
              0,
              0);

          Assert.That(purchasePricePercent, Is.EqualTo(100).Within(0.001));
          Assert.That(result.RemainingCleanCost, Is.EqualTo(250).Within(0.001));
          Assert.That(result.AverageBuyPricePercent, Is.EqualTo(100).Within(0.001));
          Assert.That(result.UnrealizedPnl, Is.EqualTo(0).Within(0.001));
      }

      [Test]
      public void CurrencyPosition_WithStaleTradeNominal_UsesUnchangedIssueNominalForCost()
      {
          const double purchasePrice = 996;
          const double storedTradeFaceValue = 100;
          const double issueFaceValue = 1_000;
          const double cnyRateToRub = 11.6504;
          const double currentPrice = 998.924;

          var purchasePricePercent = PortfolioMarketPriceCalculator.CalculateTradePricePercent(
              purchasePrice,
              storedTradeFaceValue,
              issueFaceValue,
              issueFaceValue,
              "CNY",
              "CNY",
              cnyRateToRub,
              cnyRateToRub);
          var purchasePriceAtCurrentNominal = PortfolioMarketPriceCalculator.Calculate(
              issueFaceValue,
              "CNY",
              "CNY",
              purchasePricePercent,
              cnyRateToRub,
              cnyRateToRub);
          var income = PortfolioPositionCalculator.Calculate(
              [new PositionIncomeTrade(
                  new DateOnly(2026, 3, 16),
                  TradeSide.Buy,
                  25,
                  purchasePriceAtCurrentNominal!.Value,
                  10.105,
                  purchasePricePercent)],
              new DateOnly(2026, 9, 26),
                  currentPrice,
                  19.7,
                  91);
          var averageBuyPriceAtCurrentNominal = income.RemainingCleanCost / income.RemainingQuantity;
          var valuation = PortfolioPositionCalculator.Calculate(
              new PortfolioPositionRecord(
                  "RU000A1089K2",
                  "TQOY",
                  "CNY",
                  25,
                  25,
                  0,
                  24_900,
                  24_910.105,
                  0,
                  purchasePrice),
              currentPrice,
              averageBuyPriceAtCurrentNominal);

          Assert.Multiple(() =>
          {
              Assert.That(purchasePricePercent, Is.EqualTo(99.6).Within(0.001));
              Assert.That(purchasePriceAtCurrentNominal, Is.EqualTo(996).Within(0.001));
              Assert.That(averageBuyPriceAtCurrentNominal, Is.EqualTo(996).Within(0.001));
              Assert.That(income.UnrealizedPnl, Is.EqualTo(73.1).Within(0.001));
              Assert.That(income.CouponIncome, Is.EqualTo(1_049.94505494505).Within(0.001));
              Assert.That(income.TotalPnl, Is.EqualTo(1_112.94005494505).Within(0.001));
              Assert.That(income.TotalPnlPercent, Is.EqualTo(4.46963877487973).Within(0.001));
              Assert.That(valuation.MarketValue, Is.EqualTo(24_973.1).Within(0.001));
              Assert.That(valuation.UnrealizedPnl, Is.EqualTo(73.1).Within(0.001));
          });
      }

      [Test]
      public void TradePricePercent_WhenNominalChanged_UsesNominalStoredForTrade()
      {
          var purchasePricePercent = PortfolioMarketPriceCalculator.CalculateTradePricePercent(
              500,
              500,
              1_000,
              250,
              "RUB",
              "RUB",
              null,
              null);

          Assert.That(purchasePricePercent, Is.EqualTo(100).Within(0.001));
      }

      [Test]
      public void ApproximateIncome_UsesHoldingDaysAndSubtractsPaidBuyCommission()
      {
          var result = PortfolioPositionCalculator.Calculate(
          [
              new PositionIncomeTrade(new DateOnly(2026, 8, 16), TradeSide.Buy, 1, 1_000, 4)
          ],
          new DateOnly(2026, 8, 28),
          990,
          30,
          30);

          Assert.That(result.CouponIncome, Is.EqualTo(12).Within(0.001));
          Assert.That(result.TotalPnl, Is.EqualTo(-2).Within(0.001));
          Assert.That(result.TotalPnlPercent, Is.EqualTo(-0.2).Within(0.001));
      }

      [Test]
      public void ApproximateIncome_AppliesSalesToOldestLotsFirst()
      {
          var result = PortfolioPositionCalculator.Calculate(
          [
              new PositionIncomeTrade(new DateOnly(2026, 8, 1), TradeSide.Buy, 2, 1_000, 4),
              new PositionIncomeTrade(new DateOnly(2026, 8, 10), TradeSide.Buy, 2, 900, 2),
              new PositionIncomeTrade(new DateOnly(2026, 8, 20), TradeSide.Sell, 3, 950, 3)
          ],
          new DateOnly(2026, 8, 28),
          890,
          30,
          30);

          Assert.That(result.RemainingCleanCost, Is.EqualTo(900).Within(0.001));
          Assert.That(result.PaidBuyCommission, Is.EqualTo(1).Within(0.001));
          Assert.That(result.CouponIncome, Is.EqualTo(18).Within(0.001));
          Assert.That(result.TotalPnl, Is.EqualTo(7).Within(0.001));
      }

    [Test]
    public void Cash_UsesLatestCurrencySnapshotAndTradesAfterIt()
    {
        var balance = PortfolioCashCalculator.Calculate(
            [
                new CashSnapshot("RUB", new DateOnly(2026, 1, 1), 100_000),
                new CashSnapshot("CNY", new DateOnly(2026, 1, 1), 2_000),
                new CashSnapshot("RUB", new DateOnly(2026, 1, 10), 80_000)
            ],
            [
                new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 10), TradeSide.Buy, 10, 100),
                new PortfolioTrade("RU000A", "TQCB", "RUB", new DateOnly(2026, 1, 11), TradeSide.Sell, 2, 150),
                new PortfolioTrade("CN000A", "TQCB", "CNY", new DateOnly(2026, 1, 11), TradeSide.Buy, 3, 200)
            ],
            new DateOnly(2026, 1, 11));

        Assert.That(balance.Single(x => x.CurrencyId == "RUB").Amount, Is.EqualTo(80_300).Within(0.001));
        Assert.That(balance.Single(x => x.CurrencyId == "CNY").Amount, Is.EqualTo(1_400).Within(0.001));
    }

    [Test]
    public void MoneyMarketFundOperations_ChangeRubCashWithCommission()
    {
        var operations = new[]
        {
            new MoneyMarketFundOperation("LQDT", new DateOnly(2026, 1, 2), TradeSide.Sell, 1_000, 1, 40),
            new MoneyMarketFundOperation("LQDT", new DateOnly(2026, 1, 3), TradeSide.Buy, 3_000, 1, 120)
        };

        Assert.That(MoneyMarketFundOperationCalculator.CashDelta(operations), Is.EqualTo(-2_160));
        Assert.That(MoneyMarketFundOperationCalculator.QuantityDelta(operations), Is.EqualTo(2_000));
    }
}
