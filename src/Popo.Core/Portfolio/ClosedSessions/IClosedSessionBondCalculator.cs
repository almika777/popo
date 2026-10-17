namespace Popo.Core.Portfolio.ClosedSessions;

public interface IClosedSessionBondCalculator
{
    string Kind { get; }
    ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings);
}
