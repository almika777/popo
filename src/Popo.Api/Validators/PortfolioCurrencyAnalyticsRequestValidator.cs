using FluentValidation;
using Popo.Api.Models;

namespace Popo.Api.Validators;

/// <summary>Проверяет временные параметры валютной аналитики портфеля.</summary>
public sealed class PortfolioCurrencyAnalyticsRequestValidator : AbstractValidator<PortfolioCurrencyAnalyticsRequest>
{
    public PortfolioCurrencyAnalyticsRequestValidator()
    {
        RuleFor(request => request.CashBalances)
            .NotNull()
            .Must(HaveUniqueCashCurrencies)
            .WithMessage("Укажите каждую валюту денежного остатка не более одного раза.");
        RuleForEach(request => request.CashBalances).ChildRules(balance =>
        {
            balance.RuleFor(x => x.CurrencyId)
                .Must(IsNotBlank)
                .WithMessage("Укажите валюту денежного остатка.");
            balance.RuleFor(x => x.Amount)
                .Must(IsFiniteNonNegative)
                .WithMessage("Денежный остаток должен быть конечным и неотрицательным.");
        });

        RuleFor(request => request.ManualPrices)
            .NotNull()
            .Must(HaveUniqueAssetIds)
            .WithMessage("Укажите каждую ручную цену актива не более одного раза.");
        RuleForEach(request => request.ManualPrices).ChildRules(price =>
        {
            price.RuleFor(x => x.AssetId)
                .Must(IsNotBlank)
                .WithMessage("Укажите идентификатор актива для ручной цены.");
            price.RuleFor(x => x.Price)
                .Must(IsFinitePositive)
                .WithMessage("Цена одной бумаги должна быть конечной и положительной.");
        });

        RuleFor(request => request.ManualRates)
            .NotNull()
            .Must(HaveUniqueRateCurrencies)
            .WithMessage("Укажите ручной курс каждой валюты не более одного раза.");
        RuleForEach(request => request.ManualRates).ChildRules(rate =>
        {
            rate.RuleFor(x => x.CurrencyId)
                .Must(IsNotBlank)
                .WithMessage("Укажите валюту для ручного курса.");
            rate.RuleFor(x => x.RateToRub)
                .Must(IsFinitePositive)
                .WithMessage("Курс должен быть конечным и положительным.");
        });
    }

    private static bool HaveUniqueCashCurrencies(
        IReadOnlyList<PortfolioCurrencyAnalyticsCashBalanceRequest>? balances) =>
        balances is not null
        && balances.All(balance => balance is not null && IsNotBlank(balance.CurrencyId))
        && balances.Select(balance => balance.CurrencyId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == balances.Count;

    private static bool HaveUniqueAssetIds(
        IReadOnlyList<PortfolioCurrencyAnalyticsManualPriceRequest>? prices) =>
        prices is not null
        && prices.All(price => price is not null && IsNotBlank(price.AssetId))
        && prices.Select(price => price.AssetId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == prices.Count;

    private static bool HaveUniqueRateCurrencies(
        IReadOnlyList<PortfolioCurrencyAnalyticsManualRateRequest>? rates) =>
        rates is not null
        && rates.All(rate => rate is not null && IsNotBlank(rate.CurrencyId))
        && rates.Select(rate => rate.CurrencyId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == rates.Count;

    private static bool IsNotBlank(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsFiniteNonNegative(double value) => double.IsFinite(value) && value >= 0;

    private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;
}
