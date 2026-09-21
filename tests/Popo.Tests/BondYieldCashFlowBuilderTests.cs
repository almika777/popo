using NUnit.Framework;
using Popo.Core.Calculators;
using Popo.Core.Recommendations;

namespace Popo.Tests;

[TestFixture]
public sealed class BondYieldCashFlowBuilderTests
{
    [Test]
    public void TryBuild_PreservesPublishedCouponValue()
    {
        var request = MaturityRequest();

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [Coupon(request.YieldDate, request.YieldDate.AddDays(-182), value: 80d, rate: 99d)],
            []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(80d, request.YieldDate),
            new CashFlow(1_000d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_ProjectsUnknownCouponsFromLastKnownValueAndRemainingFace()
    {
        var request = MaturityRequest(
            settlementDate: new DateOnly(2026, 1, 2),
            yieldDate: new DateOnly(2027, 1, 1));
        var coupons = new[]
        {
            Coupon(new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1), value: 100d),
            Coupon(new DateOnly(2026, 7, 1), new DateOnly(2026, 1, 1)),
            Coupon(new DateOnly(2027, 1, 1), new DateOnly(2026, 7, 1))
        };
        var principals = new[]
        {
            Principal(new DateOnly(2026, 7, 1), value: 400d)
        };

        var schedule = BondYieldCashFlowBuilder.TryBuild(request, coupons, principals);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(100d, new DateOnly(2026, 7, 1)),
            new CashFlow(400d, new DateOnly(2026, 7, 1)),
            new CashFlow(60d, new DateOnly(2027, 1, 1)),
            new CashFlow(600d, new DateOnly(2027, 1, 1))
        }).Using(CashFlowComparer.WithTolerance(1e-10)));
    }

    [Test]
    public void TryBuild_UsesLastKnownCouponAmountWithoutPeriodAnnualization()
    {
        var request = MaturityRequest(
            settlementDate: new DateOnly(2026, 8, 31),
            yieldDate: new DateOnly(2026, 11, 1));

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [
                Coupon(new DateOnly(2026, 8, 28), new DateOnly(2026, 7, 29), value: 14.33d),
                Coupon(new DateOnly(2026, 9, 27), new DateOnly(2026, 8, 28)),
                Coupon(new DateOnly(2026, 10, 28), new DateOnly(2026, 9, 27))
            ],
            []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows[0], Is.EqualTo(new CashFlow(14.33d, new DateOnly(2026, 9, 27))));
    }

    [Test]
    public void TryBuild_ExposesCashFlowTypesAndMarksOnlyReconstructedCouponsAsProjected()
    {
        var request = MaturityRequest(
            settlementDate: new DateOnly(2026, 1, 2),
            yieldDate: new DateOnly(2027, 1, 1));

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [
                Coupon(new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1), value: 100d),
                Coupon(new DateOnly(2026, 7, 1), new DateOnly(2026, 1, 1)),
                Coupon(new DateOnly(2027, 1, 1), new DateOnly(2026, 7, 1), value: 30d)
            ],
            [Principal(new DateOnly(2026, 7, 1), value: 400d)]);

        Assert.That(schedule, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(schedule!.CashFlowDetails.Select(x => (x.Date, x.Type, x.IsProjected)), Is.EqualTo(new[]
            {
                (new DateOnly(2026, 7, 1), BondYieldCashFlowType.Coupon, true),
                (new DateOnly(2026, 7, 1), BondYieldCashFlowType.Amortization, false),
                (new DateOnly(2027, 1, 1), BondYieldCashFlowType.Coupon, false),
                (new DateOnly(2027, 1, 1), BondYieldCashFlowType.Redemption, false)
            }));
            Assert.That(schedule.CashFlowDetails.Select(x => x.Amount), Is.EqualTo(new[]
            {
                100d, 400d, 30d, 600d
            }).Within(1e-10));
        });
    }

    [Test]
    public void TryBuild_UsesCouponBeforeAmortizationOnSameDate()
    {
        var request = MaturityRequest(
            settlementDate: new DateOnly(2026, 1, 2),
            yieldDate: new DateOnly(2027, 1, 1));

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [
                Coupon(new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1), value: 100d),
                Coupon(new DateOnly(2026, 7, 1), new DateOnly(2026, 1, 1))
            ],
            [Principal(new DateOnly(2026, 7, 1), value: 400d)]);

        Assert.That(schedule, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(schedule!.CashFlows[0].ValueInRub, Is.EqualTo(100d).Within(1e-10));
            Assert.That(schedule.CashFlows[1], Is.EqualTo(new CashFlow(400d, new DateOnly(2026, 7, 1))));
        });
    }

    [Test]
    public void TryBuild_PrefersPublishedAmortizationValueAndAddsRemainingRedemptionOnce()
    {
        var request = MaturityRequest(requiresCouponSchedule: false);
        var amortizationDate = request.YieldDate.AddDays(-90);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [],
            [Principal(amortizationDate, value: 100d, percent: 50d)]);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(100d, amortizationDate),
            new CashFlow(900d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_AppliesMultipleAmortizationsSequentially()
    {
        var request = MaturityRequest(requiresCouponSchedule: false);
        var firstDate = request.YieldDate.AddDays(-180);
        var secondDate = request.YieldDate.AddDays(-90);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [],
            [
                Principal(firstDate, value: 200d),
                Principal(secondDate, value: 300d)
            ]);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(200d, firstDate),
            new CashFlow(300d, secondDate),
            new CashFlow(500d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_TreatsPublishedZeroAmortizationAsKnownFlow()
    {
        var request = MaturityRequest(requiresCouponSchedule: false);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [],
            [Principal(request.YieldDate.AddDays(-90), value: 0d)]);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(1_000d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_ExcludesFlowsOnOrBeforeSettlementDate()
    {
        var request = MaturityRequest(requiresCouponSchedule: false);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [Coupon(request.SettlementDate, request.SettlementDate.AddDays(-30), value: 25d)],
            [Principal(request.SettlementDate, value: 100d)]);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(1_000d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_AddsAccruedCouponAndOfferRedemptionBetweenCouponDates()
    {
        var settlementDate = new DateOnly(2026, 2, 1);
        var offerDate = new DateOnly(2026, 4, 1);
        var request = new BondYieldRequest(
            "SEC", "TQCB", "ISIN", settlementDate, offerDate,
            BondYieldDateType.Offer, 1_000d, "RUB", "RUB", 98d, true);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [
                Coupon(new DateOnly(2026, 1, 1), new DateOnly(2025, 7, 1), value: 50.41095890410959d),
                Coupon(new DateOnly(2026, 7, 1), new DateOnly(2026, 1, 1))
            ],
            []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(24.65753424657534d, offerDate),
            new CashFlow(980d, offerDate)
        }).Using(CashFlowComparer.WithTolerance(1e-10)));
    }

    [Test]
    public void TryBuild_DoesNotMarkPublishedOfferPeriodCouponAsProjected()
    {
        var settlementDate = new DateOnly(2026, 2, 1);
        var offerDate = new DateOnly(2026, 4, 1);
        var request = new BondYieldRequest(
            "SEC", "TQCB", "ISIN", settlementDate, offerDate,
            BondYieldDateType.Offer, 1_000d, "RUB", "RUB", 100d, true);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [Coupon(new DateOnly(2026, 7, 1), new DateOnly(2026, 1, 1), value: 50d)],
            []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlowDetails[0].Type, Is.EqualTo(BondYieldCashFlowType.Coupon));
        Assert.That(schedule.CashFlowDetails[0].IsProjected, Is.False);
    }

    [Test]
    public void TryBuild_ReturnsNullForOfferWithoutPrice()
    {
        var request = MaturityRequest() with
        {
            YieldDateType = BondYieldDateType.Offer,
            OfferPricePercent = null
        };

        var schedule = BondYieldCashFlowBuilder.TryBuild(request, [], []);

        Assert.That(schedule, Is.Null);
    }

    [Test]
    public void TryBuild_SupportsDiscountBondWithoutCouponSchedule()
    {
        var request = MaturityRequest(requiresCouponSchedule: false);

        var schedule = BondYieldCashFlowBuilder.TryBuild(request, [], []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(1_000d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_RejectsStructuredBondWithoutExplicitRedemption()
    {
        var request = MaturityRequest(requiresCouponSchedule: false) with
        {
            RequiresExplicitRedemption = true
        };

        var schedule = BondYieldCashFlowBuilder.TryBuild(request, [], []);

        Assert.That(schedule, Is.Null);
    }

    [Test]
    public void TryBuild_AcceptsStructuredBondWithExplicitMaturityRedemption()
    {
        var request = MaturityRequest(requiresCouponSchedule: false) with
        {
            RequiresExplicitRedemption = true
        };

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [],
            [Principal(request.YieldDate, value: 1_000d)]);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(1_000d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_UsesCurrentIndexedSettlementFaceAsFrozenRedemption()
    {
        var request = MaturityRequest(requiresCouponSchedule: false) with
        {
            SettlementFaceValue = 1_125.40d
        };

        var schedule = BondYieldCashFlowBuilder.TryBuild(request, [], []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows, Is.EqualTo(new[]
        {
            new CashFlow(1_125.40d, request.YieldDate)
        }));
    }

    [Test]
    public void TryBuild_ReturnsNullWhenRequiredCouponScheduleCannotBeReconstructed()
    {
        var schedule = BondYieldCashFlowBuilder.TryBuild(MaturityRequest(), [], []);

        Assert.That(schedule, Is.Null);
    }

    [Test]
    public void TryBuild_UsesActualDaysAcrossLeapDay()
    {
        var request = MaturityRequest(
            settlementDate: new DateOnly(2024, 2, 29),
            yieldDate: new DateOnly(2024, 8, 31));

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [
                Coupon(new DateOnly(2024, 2, 28), new DateOnly(2023, 2, 28), rate: 10d),
                Coupon(new DateOnly(2024, 8, 31), new DateOnly(2024, 2, 28))
            ],
            []);

        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!.CashFlows[0].ValueInRub, Is.EqualTo(50.68493150684932d).Within(1e-10));
    }

    [Test]
    public void TryBuild_ReconstructsRu000A108Gr8AndProducesExpectedEy()
    {
        var settlementDate = new DateOnly(2026, 8, 20);
        var maturityDate = new DateOnly(2027, 5, 20);
        var request = MaturityRequest(settlementDate, maturityDate);

        var schedule = BondYieldCashFlowBuilder.TryBuild(
            request,
            [
                Coupon(settlementDate, new DateOnly(2026, 5, 21), value: 38.66d),
                Coupon(new DateOnly(2026, 11, 19), settlementDate),
                Coupon(new DateOnly(2027, 2, 18), new DateOnly(2026, 11, 19)),
                Coupon(maturityDate, new DateOnly(2027, 2, 18))
            ],
            []);
        var yield = schedule is null
            ? null
            : YieldCalculator.TryCalculate(settlementDate, 1_001.90d, schedule.CashFlows);

        Assert.Multiple(() =>
        {
            Assert.That(schedule, Is.Not.Null);
            Assert.That(yield, Is.EqualTo(16.13d).Within(0.005d));
        });
    }

    private static BondYieldRequest MaturityRequest(
        DateOnly? settlementDate = null,
        DateOnly? yieldDate = null,
        bool requiresCouponSchedule = true) => new(
        "SEC",
        "TQCB",
        "ISIN",
        settlementDate ?? new DateOnly(2026, 8, 20),
        yieldDate ?? new DateOnly(2027, 8, 20),
        BondYieldDateType.Maturity,
        1_000d,
        "RUB",
        "RUB",
        null,
        requiresCouponSchedule);

    private static BondCouponSource Coupon(
        DateOnly date,
        DateOnly periodStart,
        double? value = null,
        double? rate = null,
        double faceValue = 1_000d) => new(date, periodStart, value, faceValue, rate);

    private static BondPrincipalSource Principal(
        DateOnly date,
        double? value = null,
        double? percent = null) => new(date, value, percent, 1_000d);

    private sealed class CashFlowComparer(double tolerance) : IEqualityComparer<CashFlow>
    {
        public static CashFlowComparer WithTolerance(double tolerance) => new(tolerance);

        public bool Equals(CashFlow x, CashFlow y) =>
            x.Date == y.Date && Math.Abs(x.ValueInRub - y.ValueInRub) <= tolerance;

        public int GetHashCode(CashFlow obj) => HashCode.Combine(obj.Date, obj.ValueInRub);
    }
}
