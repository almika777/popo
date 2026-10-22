using FluentValidation;
using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Validators;

public sealed class UpsertMoneyMarketFundRequestValidator : AbstractValidator<UpsertMoneyMarketFundRequest>
{
    public UpsertMoneyMarketFundRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.SecId)
            .Must(secId => !string.IsNullOrWhiteSpace(secId)
                && RelationHelper.MoneyMarketFunds.ContainsKey(secId.Trim()))
            .WithMessage("Поддерживаются только LQDT и TMON.");
        RuleFor(request => request.Quantity)
            .Must(quantity => double.IsFinite(quantity) && quantity > 0 && quantity == Math.Truncate(quantity))
            .WithMessage("Количество должно быть положительным целым числом.");
        RuleFor(request => request.AveragePrice)
            .Must(price => double.IsFinite(price) && price > 0)
            .WithMessage("Средняя цена должна быть положительной.");
    }
}
