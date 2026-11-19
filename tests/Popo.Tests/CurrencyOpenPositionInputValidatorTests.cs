using NUnit.Framework;
using Popo.Api.Services.Position;
using Popo.Api.Validators;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

public sealed class CurrencyOpenPositionInputValidatorTests
{
    [Test]
    public void Validate_RejectsMissingCurrentCurrencyRate()
    {
        var input = new OpenPositionBuildInput
        {
            Trades = [new PortfolioTradeRecord(Guid.NewGuid(), "NO-RATE", "TQCB", "RUB", new DateOnly(2026, 1, 11),
                TradeSide.Buy, 1, 85_000, 1_000, 0, 0, 0, 85_000)],
            Security = new MoexBond { SecId = "NO-RATE", BoardId = "TQCB", FaceValue = 1_000, FaceUnit = "USD",
                BondType = "Валютная облигация" },
            Marketdata = new MoexMarketdata { SecId = "NO-RATE", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 1, 20),
            Coupons = [new OpenPositionCoupon("NO-RATE", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD")]
        };

        var result = new CurrencyOpenPositionInputValidator().Validate(input);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Select(error => error.ErrorMessage), Does.Contain("Нет текущего курса для оценки позиции."));
    }

    [Test]
    public void Validate_RejectsMissingPaidCouponInRubles()
    {
        var input = new OpenPositionBuildInput
        {
            Trades = [new PortfolioTradeRecord(Guid.NewGuid(), "NO-PAID-COUPON", "TQCB", "RUB", new DateOnly(2026, 1, 21),
                TradeSide.Buy, 1, 85_000, 1_000, 0, 0, 0, 85_000)],
            Security = new MoexBond { SecId = "NO-PAID-COUPON", BoardId = "TQCB", FaceValue = 1_000, FaceUnit = "USD",
                BondType = "Валютная облигация" },
            Marketdata = new MoexMarketdata { SecId = "NO-PAID-COUPON", BoardId = "TQCB", LCurrentPrice = 100 },
            AsOf = new DateOnly(2026, 2, 9),
            Coupons =
            [
                new OpenPositionCoupon("NO-PAID-COUPON", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 30, "USD"),
                new OpenPositionCoupon("NO-PAID-COUPON", new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 2), 30, "USD")
            ],
            CurrentCurrencyRate = new PortfolioCurrencyRateRecord("USD", new DateOnly(2026, 2, 9), 90)
        };

        var result = new CurrencyOpenPositionInputValidator().Validate(input);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Select(error => error.ErrorMessage), Does.Contain("Неизвестна сумма купона на 2026-01-31."));
    }
}
