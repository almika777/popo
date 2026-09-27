using FluentValidation;
using Popo.Api.Models;

namespace Popo.Api.Validators;

public sealed class UpsertPortfolioValuationRequestValidator : AbstractValidator<UpsertPortfolioValuationRequest>
{
    public UpsertPortfolioValuationRequestValidator()
    {
        RuleFor(request => request.TotalValue)
            .Must(value => double.IsFinite(value) && value >= 0)
            .WithMessage("Стоимость портфеля должна быть неотрицательной.");
    }
}

public sealed class UpsertPortfolioCashFlowRequestValidator : AbstractValidator<UpsertPortfolioCashFlowRequest>
{
    public UpsertPortfolioCashFlowRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Type)
            .IsInEnum()
            .WithMessage("Тип операции должен быть: пополнение, вывод или налог.");
        RuleFor(request => request.Amount)
            .Must(amount => double.IsFinite(amount) && amount > 0)
            .WithMessage("Сумма должна быть положительной.");
    }
}

public sealed class PortfolioReturnQueryValidator : AbstractValidator<PortfolioReturnQuery>
{
    public PortfolioReturnQueryValidator()
    {
        RuleFor(request => request.To)
            .Must((request, to) => to > request.From)
            .WithMessage("Конечная дата должна быть позже начальной.");
    }
}
