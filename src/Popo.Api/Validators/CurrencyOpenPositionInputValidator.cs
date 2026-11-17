using FluentValidation;
using Popo.Api.Services.Position;
using Popo.Core.Portfolio;

namespace Popo.Api.Validators;

public sealed class CurrencyOpenPositionInputValidator : AbstractValidator<OpenPositionBuildInput>
{
    public CurrencyOpenPositionInputValidator()
    {
        RuleFor(input => input).Custom(ValidateInput);
    }

    private static void ValidateInput(OpenPositionBuildInput input, ValidationContext<OpenPositionBuildInput> context)
    {
        if (input.Trades.Count == 0)
        {
            context.AddFailure("Для расчёта позиции нужны сделки.");
            return;
        }

        var currency = input.Trades[0].CurrencyId;
        if (string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(input.Security.FaceUnit))
        {
            context.AddFailure("Не указана валюта позиции или номинала.");
            return;
        }

        if (!double.IsFinite(input.Security.FaceValue) || input.Security.FaceValue <= 0
            || input.Marketdata.CurrentPrice is not { } price || !double.IsFinite(price) || price <= 0)
            context.AddFailure("Нет актуального номинала или цены облигации.");

        if (!HasConversion(input, input.Security.FaceUnit, currency, input.AsOf))
            context.AddFailure("Нет текущего курса для оценки позиции.");

        var trades = input.Trades.OrderBy(trade => trade.TradeDate).ThenBy(trade => trade.Id).ToArray();
        var remaining = 0;
        var start = 0;
        for (var i = 0; i < trades.Length; i++)
        {
            remaining += trades[i].Side == TradeSide.Buy ? trades[i].Quantity : -trades[i].Quantity;
            if (remaining == 0)
                start = i + 1;
        }
        if (remaining <= 0 || start >= trades.Length)
        {
            context.AddFailure("Нет открытого остатка позиции.");
            return;
        }

        var from = trades[start].TradeDate;
        var through = from.DayNumber;
        foreach (var period in input.Coupons.Where(period => period.StartDate <= input.AsOf && period.CouponDate > from)
                     .OrderBy(period => period.StartDate))
        {
            if (period.CouponDate <= period.StartDate || period.StartDate.DayNumber > through
                || period.StartDate.DayNumber < through && through != from.DayNumber)
            {
                context.AddFailure("Купонная история содержит пробел или пересечение периодов.");
                return;
            }

            var sameCurrency = SameCurrency(period.FaceUnit, currency);
            var paidInDifferentCurrency = !sameCurrency && period.CouponDate <= input.AsOf;
            var amount = paidInDifferentCurrency ? period.ValueInRub : period.Value;
            if (string.IsNullOrWhiteSpace(period.FaceUnit) || amount is not { } value || !double.IsFinite(value) || value < 0)
                context.AddFailure($"Неизвестна сумма купона на {period.CouponDate:yyyy-MM-dd}.");

            var rateDate = paidInDifferentCurrency ? period.CouponDate : input.AsOf;
            var sourceCurrency = paidInDifferentCurrency ? "RUB" : period.FaceUnit;
            if (!HasConversion(input, sourceCurrency, currency, rateDate))
                context.AddFailure($"Нет курса для купона на {period.CouponDate:yyyy-MM-dd}.");

            through = period.CouponDate.DayNumber;
        }

        if (through <= input.AsOf.DayNumber)
            context.AddFailure("Купонная история не покрывает период владения.");
    }

    private static bool HasConversion(OpenPositionBuildInput input, string source, string target, DateOnly date)
        => SameCurrency(source, target) || HasRate(input, source, date) && HasRate(input, target, date);

    private static bool HasRate(OpenPositionBuildInput input, string currency, DateOnly date)
    {
        if (PositionCurrencyRates.IsRubleCurrency(currency))
            return true;
        var rate = input.CurrencyRates.Concat(input.CurrentCurrencyRate is null ? [] : [input.CurrentCurrencyRate])
            .Where(item => string.Equals(item.CurrencyId, currency, StringComparison.OrdinalIgnoreCase) && item.RateDate <= date)
            .OrderByDescending(item => item.RateDate).FirstOrDefault();
        return rate is not null && double.IsFinite(rate.RateToRub) && rate.RateToRub > 0;
    }

    private static bool SameCurrency(string first, string second)
        => string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
           || PositionCurrencyRates.IsRubleCurrency(first) && PositionCurrencyRates.IsRubleCurrency(second);
}
