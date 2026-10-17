namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class ZoClosedSessionBondCalculator : IClosedSessionBondCalculator
{
    public string Kind => "Zo";

    public ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings)
        => new ClosedSessionCouponAccrualCalculator().Calculate(instrument, holdings, preferRubValue: true);
}
