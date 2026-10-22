using Popo.Core.Bonds;

namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class ClosedSessionBondCalculatorFactory
{
    /// <summary>Выбирает расчёт купонов для ЗО, флоатера или обычной облигации по параметрам инструмента.</summary>
    public IClosedSessionBondCalculator Create(ClosedSessionInstrument instrument)
    {
        if (ClosedSessionCurrencyConverter.IsRuble(instrument.CurrencyId)
            && instrument.FaceUnit.ToUpperInvariant() is "USD" or "EUR" or "CNY")
            return new ZoClosedSessionBondCalculator();
        return BondCouponClassifier.IsFloater(instrument.BondType, instrument.BondSubType)
            ? new FloaterClosedSessionBondCalculator()
            : new DefaultClosedSessionBondCalculator();
    }
}
