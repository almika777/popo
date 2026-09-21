using NUnit.Framework;
using Popo.Core.Calculators;
using Popo.Core.Recommendations;
using Popo.Storage.Entities;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers;

namespace Popo.Tests;

[TestFixture]
public sealed class CashFlowProviderTests
{
    private static readonly DateOnly Today = new(2026, 8, 18);

    [TestCase("RUB", "RUB", 1d)]
    [TestCase("SUR", "RUR", 1d)]
    [TestCase("USD", "RUB", 92.5d)]
    [TestCase("RUB", "USD", 1d / 92.5d)]
    [TestCase("USD", "CNY", 7.4d)]
    public void TryGetCurrencyConversionFactor_ConvertsFaceCurrencyToSettlementCurrency(
        string sourceCurrency,
        string settlementCurrency,
        double expectedFactor)
    {
        var rates = new Dictionary<string, CurrencyRateEntity>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = Rate("USD", unitRate: 92.5d),
            ["CNY"] = Rate("CNY", unitRate: 12.5d)
        };

        var success = CashFlowProvider.TryGetCurrencyConversionFactor(
            sourceCurrency,
            settlementCurrency,
            rates,
            out var factor);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(factor, Is.EqualTo(expectedFactor).Within(1e-12));
        });
    }

    [Test]
    public void TryGetCurrencyConversionFactor_ReturnsFalseWhenSnapshotRateIsMissing()
    {
        var success = CashFlowProvider.TryGetCurrencyConversionFactor(
            "EUR",
            "USD",
            new Dictionary<string, CurrencyRateEntity>(),
            out _);

        Assert.That(success, Is.False);
    }

    [Test]
    public void CompleteFutureCoupons_UsesKnownPercentAndRemainingFaceAfterAmortization()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 50, valuePercent: 5),
            Coupon(new DateOnly(2026, 11, 20)),
            Coupon(new DateOnly(2027, 2, 20))
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2026, 11, 20), value: 500, valuePercent: 50)
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, amorts, Today);

        Assert.Multiple(() =>
        {
            Assert.That(coupons[1].Value, Is.EqualTo(50).Within(0.0001));
            Assert.That(coupons[1].ValuePercent, Is.EqualTo(5).Within(0.0001));
            Assert.That(coupons[2].Value, Is.EqualTo(25).Within(0.0001));
            Assert.That(coupons[2].ValuePercent, Is.EqualTo(5).Within(0.0001));
        });
    }

    [Test]
    public void CompleteFutureCoupons_DerivesPercentFromKnownCouponValue()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 80),
            Coupon(new DateOnly(2026, 11, 20))
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, [], Today);

        Assert.Multiple(() =>
        {
            Assert.That(coupons[1].Value, Is.EqualTo(80).Within(0.0001));
            Assert.That(coupons[1].ValuePercent, Is.EqualTo(8).Within(0.0001));
        });
    }

    [Test]
    public void CompleteFutureCoupons_PreservesKnownFutureCouponAndUsesItsPercentAfterwards()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 50, valuePercent: 5),
            Coupon(new DateOnly(2026, 11, 20), value: 70, valuePercent: 7),
            Coupon(new DateOnly(2027, 2, 20))
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, [], Today);

        Assert.Multiple(() =>
        {
            Assert.That(coupons[1].Value, Is.EqualTo(70).Within(0.0001));
            Assert.That(coupons[1].ValuePercent, Is.EqualTo(7).Within(0.0001));
            Assert.That(coupons[2].Value, Is.EqualTo(70).Within(0.0001));
            Assert.That(coupons[2].ValuePercent, Is.EqualTo(7).Within(0.0001));
        });
    }

    [Test]
    public void CompleteFutureCoupons_PrefersPublishedAmortizationAmountOverPercent()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 50, valuePercent: 5),
            Coupon(new DateOnly(2027, 2, 20))
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2026, 11, 20), value: 100, valuePercent: 50)
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, amorts, Today);

        Assert.That(coupons[1].Value, Is.EqualTo(45).Within(0.0001));
    }

    [Test]
    public void CompleteFutureCoupons_AccumulatesMultipleAmortizationsBeforeCoupon()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 50, valuePercent: 5),
            Coupon(new DateOnly(2027, 2, 20))
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2026, 9, 20), value: 200, valuePercent: 20),
            Amortization(new DateOnly(2026, 11, 20), value: 300, valuePercent: 30)
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, amorts, Today);

        Assert.Multiple(() =>
        {
            Assert.That(coupons[1].Value, Is.EqualTo(25).Within(0.0001));
            Assert.That(coupons[1].ValuePercent, Is.EqualTo(5).Within(0.0001));
        });
    }

    [Test]
    public void CompleteFutureCoupons_PreservesKnownFutureCouponAfterAmortization()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 50, valuePercent: 5),
            Coupon(new DateOnly(2027, 2, 20), value: 50, valuePercent: 5)
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2026, 11, 20), value: 500, valuePercent: 50)
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, amorts, Today);

        Assert.That(coupons[1].Value, Is.EqualTo(50).Within(0.0001));
    }

    [Test]
    public void CompleteFutureCoupons_PreservesPublishedFutureCouponWithoutAmortization()
    {
        var couponDate = new DateOnly(2026, 9, 18);
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(
                couponDate,
                value: 12.53,
                valuePercent: 15.25,
                startDate: couponDate.AddDays(-30))
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, [], Today);

        Assert.That(coupons[0].Value, Is.EqualTo(12.53));
    }

    [Test]
    public void CompleteFutureCoupons_UsesAmortizationAmountWhenPercentIsMissing()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 100, valuePercent: 10),
            Coupon(new DateOnly(2027, 2, 20))
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2026, 11, 20), value: 250)
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, amorts, Today);

        Assert.That(coupons[1].Value, Is.EqualTo(75).Within(0.0001));
    }

    [Test]
    public void CompleteFutureCoupons_UsesAnnualPercentAndActualCouponPeriod()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(
                new DateOnly(2026, 11, 18),
                valuePercent: 17.5,
                startDate: new DateOnly(2026, 5, 20),
                isin: "RU000A0JXS59")
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, [], Today, couponFrequency: 2);

        Assert.That(coupons[0].Value, Is.EqualTo(87.26).Within(0.01));
    }

    [Test]
    public void CompleteFutureCoupons_FallsBackToCouponFrequencyWithoutPeriodDates()
    {
        var couponDate = new DateOnly(2026, 11, 18);
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(couponDate, valuePercent: 17.5, startDate: couponDate)
        };

        CashFlowProvider.CompleteFutureCoupons(coupons, [], Today, couponFrequency: 2);

        Assert.That(coupons[0].Value, Is.EqualTo(87.5).Within(0.0001));
    }

    [Test]
    public void ConvertFutureCouponsToRub_LeavesRubleCouponUnchanged()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 11, 20), value: 125, faceUnit: "RUB"),
            Coupon(new DateOnly(2026, 5, 20), value: 999, faceUnit: "RUB")
        };

        var cashFlows = CashFlowProvider.ConvertFutureCouponsToRub(
            coupons,
            new Dictionary<string, CurrencyRateEntity>(),
            Today);

        Assert.That(cashFlows, Is.EqualTo(new[] { new CashFlow(125, new DateOnly(2026, 11, 20)) }));
    }

    [Test]
    public void ConvertFutureCouponsToRub_UsesLatestKnownCurrencyRate()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 11, 20), value: 100, faceUnit: "USD")
        };
        var rates = new Dictionary<string, CurrencyRateEntity>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = Rate("USD", unitRate: 92.5)
        };

        var cashFlows = CashFlowProvider.ConvertFutureCouponsToRub(coupons, rates, Today);

        Assert.That(cashFlows, Is.EqualTo(new[] { new CashFlow(9_250, new DateOnly(2026, 11, 20)) }));
    }

    [Test]
    public void ConvertFutureCouponsToRub_UsesRateForEachCouponCurrency()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 11, 20), value: 100, faceUnit: "USD"),
            Coupon(new DateOnly(2027, 2, 20), value: 200, faceUnit: "CNY")
        };
        var rates = new Dictionary<string, CurrencyRateEntity>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = Rate("USD", unitRate: 90),
            ["CNY"] = Rate("CNY", unitRate: 12.5)
        };

        var cashFlows = CashFlowProvider.ConvertFutureCouponsToRub(coupons, rates, Today);

        Assert.That(cashFlows, Is.EqualTo(new[] {
            new CashFlow(9_000, new DateOnly(2026, 11, 20)),
            new CashFlow(2_500, new DateOnly(2027, 2, 20))
        }));
    }

    [Test]
    public void ConvertFutureCouponsToRub_SkipsPastUnknownAndCurrencyWithoutRate()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 5, 20), value: 100, faceUnit: "USD"),
            Coupon(new DateOnly(2026, 11, 20), faceUnit: "EUR"),
            Coupon(new DateOnly(2027, 2, 20), value: 100, faceUnit: "EUR")
        };

        var cashFlows = CashFlowProvider.ConvertFutureCouponsToRub(
            coupons,
            new Dictionary<string, CurrencyRateEntity>(),
            Today);

        Assert.That(cashFlows, Is.Empty);
    }

    [Test]
    public void ConvertFutureCashFlowsToRub_AddsAllFutureAmortizations()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(new DateOnly(2026, 11, 20), value: 50)
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2026, 5, 20), value: 100),
            Amortization(new DateOnly(2026, 9, 20), value: 200),
            Amortization(new DateOnly(2027, 2, 20), value: 800)
        };

        var cashFlows = CashFlowProvider.ConvertFutureCashFlowsToRub(
            coupons,
            amorts,
            new Dictionary<string, CurrencyRateEntity>(),
            Today);

        Assert.That(cashFlows, Is.EqualTo(new[]
        {
            new CashFlow(50, new DateOnly(2026, 11, 20)),
            new CashFlow(200, new DateOnly(2026, 9, 20)),
            new CashFlow(800, new DateOnly(2027, 2, 20))
        }.OrderBy(x => x.Date).ToArray()));
    }

    [Test]
    public void ConvertFutureCashFlowsToRub_ConvertsAmortizationByItsCurrencyRate()
    {
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2027, 2, 20), value: 1_000, faceUnit: "USD")
        };
        var rates = new Dictionary<string, CurrencyRateEntity>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = Rate("USD", unitRate: 90)
        };

        var cashFlows = CashFlowProvider.ConvertFutureCashFlowsToRub([], amorts, rates, Today);

        Assert.That(cashFlows, Is.EqualTo(new[]
        {
            new CashFlow(90_000, new DateOnly(2027, 2, 20))
        }));
    }

    [Test]
    public void ConvertFutureCashFlowsToRub_PrefersPublishedAmortizationAmount()
    {
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(new DateOnly(2027, 2, 20), value: 100, valuePercent: 50)
        };

        var cashFlows = CashFlowProvider.ConvertFutureCashFlowsToRub(
            [],
            amorts,
            new Dictionary<string, CurrencyRateEntity>(),
            Today);

        Assert.That(cashFlows, Is.EqualTo(new[]
        {
            new CashFlow(100, new DateOnly(2027, 2, 20))
        }));
    }

    [Test]
    public void ConvertFutureCashFlowsToRub_ExcludesCashFlowsOnValuationDate()
    {
        var coupons = new List<MoexCouponsEntity>
        {
            Coupon(Today, value: 12.74),
            Coupon(Today.AddDays(30), value: 12.53)
        };
        var amorts = new List<MoexAmortsEntity>
        {
            Amortization(Today, value: 100),
            Amortization(Today.AddDays(60), value: 1_000)
        };

        var cashFlows = CashFlowProvider.ConvertFutureCashFlowsToRub(
            coupons,
            amorts,
            new Dictionary<string, CurrencyRateEntity>(),
            Today);

        Assert.That(cashFlows, Is.EqualTo(new[]
        {
            new CashFlow(12.53, Today.AddDays(30)),
            new CashFlow(1_000, Today.AddDays(60))
        }));
    }

    private static MoexCouponsEntity Coupon(
        DateOnly date,
        double? value = null,
        double? valuePercent = null,
        string faceUnit = "RUB",
        DateOnly? startDate = null,
        string isin = "ISIN") => new()
    {
        SecId = "SEC",
        Isin = isin,
        CouponDate = date,
        StartDate = startDate ?? date.AddYears(-1),
        InitialFaceValue = 1_000,
        FaceValue = 1_000,
        FaceUnit = faceUnit,
        Value = value,
        ValuePercent = valuePercent,
        BoardId = "TQOB"
    };

    private static CurrencyRateEntity Rate(string currencyCode, double unitRate) => new()
    {
        CurrencyCode = currencyCode,
        RateDate = Today,
        UnitRate = unitRate
    };

    private static MoexAmortsEntity Amortization(
        DateOnly date,
        double? value = null,
        double? valuePercent = null,
        string faceUnit = "RUB") => new()
    {
        SecId = "SEC",
        Isin = "ISIN",
        AmortDate = date,
        InitialFaceValue = 1_000,
        FaceValue = 1_000,
        FaceUnit = faceUnit,
        Value = value,
        ValuePercent = valuePercent,
        BoardId = "TQOB"
    };
}
