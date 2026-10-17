namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class FloaterClosedSessionBondCalculator : IClosedSessionBondCalculator
{
    public string Kind => "Floater";

    // При отсутствии суммы используется последний известный купон предыдущего периода.
    public ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings)
        => new ClosedSessionCouponAccrualCalculator().Calculate(instrument, holdings,
            carryForwardMissingFloaterCoupon: true);
}
