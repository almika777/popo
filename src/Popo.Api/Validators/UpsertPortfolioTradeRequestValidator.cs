using FluentValidation;
using Popo.Api.Models;

namespace Popo.Api.Validators;

public sealed class UpsertPortfolioTradeRequestValidator : AbstractValidator<UpsertPortfolioTradeRequest>
{
    public UpsertPortfolioTradeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.SecId)
            .Must(HasText)
            .WithMessage("Укажите бумагу, торговую площадку и валюту сделки.");
        RuleFor(request => request.BoardId)
            .Must(HasText)
            .WithMessage("Укажите бумагу, торговую площадку и валюту сделки.");
        RuleFor(request => request.CurrencyId)
            .Must(HasText)
            .WithMessage("Укажите бумагу, торговую площадку и валюту сделки.");
        RuleFor(request => request.Side)
            .IsInEnum()
            .WithMessage("Тип сделки должен быть: покупка или продажа.");
        RuleFor(request => request.Quantity)
            .Must(quantity => double.IsFinite(quantity) && quantity > 0 && quantity == Math.Truncate(quantity))
            .WithMessage("Количество должно быть положительным целым числом.");
        RuleFor(request => request.Price)
            .Must(price => double.IsFinite(price) && price > 0)
            .WithMessage("Цена должна быть положительной.");
        RuleFor(request => request.FaceValue)
            .Must(value => double.IsFinite(value) && value > 0)
            .WithMessage("Номинал должен быть положительным.");
        RuleFor(request => request.AccruedInterestTotal)
            .Must(value => double.IsFinite(value) && value >= 0)
            .WithMessage("Общий НКД должен быть неотрицательным.");
        RuleFor(request => request.CommissionPercent)
            .Must(value => double.IsFinite(value) && value >= 0)
            .WithMessage("Комиссия должна быть неотрицательной.");
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}
