using FluentValidation;
using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.Portfolio;
using Popo.Core.Recommendations;

namespace Popo.Api.Validators;

public sealed class UpsertCashSnapshotRequestValidator : AbstractValidator<UpsertCashSnapshotRequest>
{
    public UpsertCashSnapshotRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.CurrencyId)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Укажите валюту.");
        RuleFor(request => request.Amount)
            .Must(amount => double.IsFinite(amount) && amount >= 0)
            .WithMessage("Сумма должна быть неотрицательной.");
    }
}

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

public sealed class AddMoneyMarketFundOperationRequestValidator : AbstractValidator<AddMoneyMarketFundOperationRequest>
{
    public AddMoneyMarketFundOperationRequestValidator()
    {
        RuleFor(request => request)
            .Must(IsValid)
            .WithMessage("Проверьте фонд, тип операции, количество, цену и комиссию.");
    }

    private static bool IsValid(AddMoneyMarketFundOperationRequest request) =>
        RelationHelper.MoneyMarketFunds.ContainsKey(request.SecId?.Trim() ?? string.Empty)
        && Enum.IsDefined(request.Side)
        && double.IsFinite(request.Quantity)
        && request.Quantity > 0
        && request.Quantity == Math.Truncate(request.Quantity)
        && double.IsFinite(request.Price)
        && request.Price > 0
        && double.IsFinite(request.Commission)
        && request.Commission >= 0;
}

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
