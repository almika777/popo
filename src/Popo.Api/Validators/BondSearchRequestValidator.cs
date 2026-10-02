using FluentValidation;
using Popo.Api.Models;

namespace Popo.Api.Validators;

public sealed class BondSearchRequestValidator : AbstractValidator<BondSearchRequest>
{
    public BondSearchRequestValidator()
    {
        RuleFor(request => request.Query)
            .Must(query => query is not null && query.Trim().Length >= 3)
            .WithMessage("Введите не менее 3 символов для поиска.");
    }
}
