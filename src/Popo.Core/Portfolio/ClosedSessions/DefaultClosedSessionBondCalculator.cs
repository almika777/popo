namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class DefaultClosedSessionBondCalculator : IClosedSessionBondCalculator
{
    public string Kind => "Default";

    public ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings)
        => new ClosedSessionCouponAccrualCalculator().Calculate(instrument, holdings);
}
