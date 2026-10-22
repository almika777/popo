namespace Popo.Core.Portfolio.ClosedSessions;

public sealed class ZoClosedSessionBondCalculator : IClosedSessionBondCalculator
{
    public string Kind => "Zo";

    /// <summary>Рассчитывает купоны ЗО, предпочитая опубликованную рублёвую сумму для рублёвых торгов.</summary>
    public ClosedSessionIncome CalculateCouponIncome(ClosedSessionInstrument instrument,
        IReadOnlyList<ClosedSessionHolding> holdings)
        => new ClosedSessionCouponAccrualCalculator().Calculate(instrument, holdings, preferRubValue: true);
}
