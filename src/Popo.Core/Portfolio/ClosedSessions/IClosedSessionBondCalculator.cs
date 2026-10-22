namespace Popo.Core.Portfolio.ClosedSessions;

public interface IClosedSessionBondCalculator
{
    string Kind { get; }

    /// <summary>Рассчитывает купонный доход выбранного типа облигации за интервалы владения.</summary>
    ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings);
}
