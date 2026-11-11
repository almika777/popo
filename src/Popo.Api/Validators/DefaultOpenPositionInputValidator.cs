using FluentValidation;
using Popo.Api.Services.Position;
using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Api.Validators;

public sealed record DefaultOpenPositionInput(
    IReadOnlyList<PortfolioTradeRecord> Trades,
    MoexMarketdata Marketdata,
    MoexBond Security);

public sealed class DefaultOpenPositionInputValidator : AbstractValidator<DefaultOpenPositionInput>
{
    public DefaultOpenPositionInputValidator()
    {
        RuleFor(x => x.Security.BondType)
            .Must(type => type is "Облигация с фиксированным (известным) купоном" or "Фикс с известным купоном")
            .WithMessage("Билдер поддерживает только облигации с известным фиксированным купоном.");
        RuleFor(x => x.Trades)
            .NotEmpty().WithMessage("Для расчёта позиции нужны сделки.");
        RuleFor(x => x.Marketdata.CurrentPrice)
            .Must(price => price is > 0 && double.IsFinite(price.Value))
            .WithMessage("Нет текущей цены облигации.");
        RuleFor(x => x.Security.FaceValue)
            .Must(value => double.IsFinite(value) && value > 0)
            .WithMessage("Нет актуального номинала облигации.");
        RuleFor(x => x.Security)
            .Must(security => security.CouponPeriod > 0 && double.IsFinite(security.CouponValue) && security.CouponValue > 0)
            .WithMessage("Нет данных для расчёта купонного дохода.");
        RuleFor(x => x)
            .Must(input => !string.IsNullOrWhiteSpace(input.Security.FaceUnit)
                           && !string.IsNullOrWhiteSpace(input.Trades[0].CurrencyId)
                           && (string.Equals(input.Security.FaceUnit, input.Trades[0].CurrencyId, StringComparison.OrdinalIgnoreCase)
                               || PositionCurrencyRates.IsRubleCurrency(input.Security.FaceUnit)
                               && PositionCurrencyRates.IsRubleCurrency(input.Trades[0].CurrencyId)))
            .When(input => input.Trades.Count > 0)
            .WithMessage("Валюта номинала должна совпадать с валютой торгов.");
        RuleFor(x => x.Trades)
            .Must(trades => trades.Sum(trade => trade.Side == TradeSide.Buy ? (long)trade.Quantity : -trade.Quantity) > 0)
            .When(input => input.Trades.Count > 0)
            .WithMessage("Нет открытого остатка позиции.");
    }
}
