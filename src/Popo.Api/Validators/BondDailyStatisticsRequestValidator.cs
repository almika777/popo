using FluentValidation;
using Popo.Api.Models;
using Popo.Core.Common;

namespace Popo.Api.Validators;

public sealed class BondDailyStatisticsRequestValidator : AbstractValidator<BondDailyStatisticsRequest>
{
    private static readonly DateOnly MinimumDate = new(2025, 1, 1);
    private readonly TimeProvider _timeProvider;

    public BondDailyStatisticsRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        RuleFor(request => request.MinimumTrades)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Минимальное количество сделок не может быть отрицательным.");

        RuleFor(request => request.MinimumTurnoverRub)
            .Must(turnover => double.IsFinite(turnover) && turnover >= 0)
            .WithMessage("Минимальный оборот должен быть неотрицательным числом.");

        RuleFor(request => request.IssuerCategory)
            .Must(category => category is null || Enum.IsDefined(category.Value))
            .WithMessage("Выбран неизвестный вид облигации.");

        RuleFor(request => request.Date)
            .GreaterThanOrEqualTo(MinimumDate)
            .WithMessage($"Выберите дату не раньше {MinimumDate:dd.MM.yyyy}.");

        RuleFor(request => request.Date)
            .Must(date => date <= GetToday())
            .WithMessage("Нельзя выбрать будущую дату.");

        RuleFor(request => request.Date)
            .Must(date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .WithMessage("Выберите будний день: в выходные торги не проводятся.");
    }

    private DateOnly GetToday() =>
        DateOnly.FromDateTime(MoscowTime.ToMoscow(_timeProvider.GetUtcNow()).DateTime);
}
