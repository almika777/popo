namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class ClosedSessionAmortizationCalculator
{
    /// <summary>Рассчитывает частичные амортизации, полученные во время владения, и сообщает, если данных недостаточно.</summary>
    public ClosedSessionIncome Calculate(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings)
    {
        double total = 0;
        double repaidNominal = 0;
        var lastDate = holdings.Count == 0 ? DateOnly.MinValue : holdings.Max(x => x.To);
        var nominalHistoryComplete = true;
        foreach (var amort in instrument.Amortizations.OrderBy(x => x.Date))
        {
            if (amort.Date >= lastDate)
                break;
            var quantity = holdings.Where(x => x.From <= amort.Date && amort.Date < x.To).Sum(x => x.Quantity);
            // Покупки в дату выплаты не дают права на возврат ранее вложенного номинала.
            quantity -= instrument.Trades.Where(x => x.Date == amort.Date && x.Side == TradeSide.Buy)
                .Sum(x => x.Quantity);
            quantity = Math.Max(0, quantity);
            var initial = amort.InitialFaceValue > 0 ? amort.InitialFaceValue : instrument.InitialFaceValue;
            var nominalValue = amort.Value is >= 0 ? amort.Value
                : amort.ValuePercent is >= 0 && initial > 0 ? initial * amort.ValuePercent / 100 : null;
            var isMaturityDate = instrument.MaturityDate.HasValue && amort.Date >= instrument.MaturityDate.Value;
            var isFinal = isMaturityDate || nominalHistoryComplete && initial > 0
                && nominalValue.HasValue && repaidNominal + nominalValue.Value >= initial - 0.000001;
            if (!nominalValue.HasValue)
                nominalHistoryComplete = false;
            else
                repaidNominal += nominalValue.Value;
            if (isFinal || quantity <= 0)
                continue;
            if (!nominalHistoryComplete || initial <= 0)
                return new ClosedSessionIncome(null, "Недостаточно данных для отделения частичной амортизации от погашения.");
            var amount = ClosedSessionCurrencyConverter.Convert(nominalValue, amort.ValueInRub,
                amort.FaceUnit, instrument.CurrencyId, amort.Date, instrument.CurrencyRates);
            if (!amount.HasValue)
                return new ClosedSessionIncome(null, $"Неизвестна сумма амортизации или курс на {amort.Date:yyyy-MM-dd}.");
            total += quantity * amount.Value;
        }
        return new ClosedSessionIncome(total);
    }
}
