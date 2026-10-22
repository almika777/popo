namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class DefaultClosedSessionBondCalculator : IClosedSessionBondCalculator
{
    public string Kind => "Default";

    /// <summary>Рассчитывает купонный доход без специальных правил переноса суммы флоатера или оценки ЗО.</summary>
    public ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings)
        => new ClosedSessionCouponAccrualCalculator().Calculate(instrument, holdings);
}
