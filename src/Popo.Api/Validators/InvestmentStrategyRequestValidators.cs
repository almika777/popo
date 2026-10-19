using FluentValidation;
using Popo.Api.Models;
using Popo.Core.Recommendations;

namespace Popo.Api.Validators;

public sealed class InvestmentStrategySettingsRequestValidator
    : AbstractValidator<InvestmentStrategySettingsRequest>
{
    public InvestmentStrategySettingsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(settings => settings.MinimumRating)
            .IsInEnum()
            .WithMessage("Выберите допустимый минимальный рейтинг.");
        RuleFor(settings => settings.MinimumMaturityDays)
            .Must(value => !value.HasValue || value.Value >= 0)
            .WithMessage("Минимальный срок до погашения не может быть отрицательным.");
        RuleFor(settings => settings.MaximumMaturityDays)
            .Must(value => !value.HasValue || value.Value >= 0)
            .WithMessage("Максимальный срок до погашения не может быть отрицательным.");
        RuleFor(settings => settings.MaximumMaturityDays)
            .Must((settings, value) => !settings.MinimumMaturityDays.HasValue
                || !value.HasValue
                || value.Value >= settings.MinimumMaturityDays.Value)
            .WithMessage("Максимальный срок должен быть не меньше минимального.");
        RuleFor(settings => settings.MinimumMedianDailyVolume)
            .Must(volume => double.IsFinite(volume) && volume >= 1d)
            .WithMessage("Минимальный медианный оборот должен быть не меньше 1.");
        RuleFor(settings => settings.OfferWindowDays)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Количество дней исключения оферты не может быть отрицательным.");
        RuleFor(settings => settings.MaximumYtm)
            .Must(IsValidYtm)
            .WithMessage("Максимальная YTM должна быть больше 0 и не больше 100%.");
        RuleFor(settings => settings.MinimumYtm)
            .Must(IsValidYtm)
            .WithMessage("Минимальная YTM должна быть больше 0 и не больше 100%.");
        RuleFor(settings => settings.MinimumYtm)
            .LessThanOrEqualTo(settings => settings.MaximumYtm)
            .WithMessage("Минимальная YTM не может быть выше максимальной.");
        RuleFor(settings => settings.InstrumentType)
            .IsInEnum()
            .WithMessage("Выберите допустимый тип облигаций.");
        RuleFor(settings => settings.FaceUnit)
            .MaximumLength(16)
            .WithMessage("Валюта номинала не должна превышать 16 символов.");
        RuleFor(settings => settings.CurrencyId)
            .MaximumLength(16)
            .WithMessage("Валюта торгов не должна превышать 16 символов.");
    }

    private static bool IsValidYtm(double value) => double.IsFinite(value) && value > 0 && value <= 100;
}

public sealed class InvestmentStrategyPresetRequestValidator : AbstractValidator<InvestmentStrategyPresetRequest>
{
    public InvestmentStrategyPresetRequestValidator(InvestmentStrategySettingsRequestValidator settingsValidator)
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Укажите название пресета.");
        RuleFor(request => request.Settings)
            .NotNull()
            .WithMessage("Параметры стратегии не переданы.");
        RuleFor(request => request.Settings)
            .SetValidator(settingsValidator)
            .When(request => request.Settings is not null);
    }
}
