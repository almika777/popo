namespace Popo.Core.Recommendations;

public enum BondCouponType { Fixed, Floating }

public enum BondCashFlowCurrency
{
    FaceUnit,
    Settlement,
    Rubles
}

public sealed record BondCashFlow(
    DateOnly Date,
    double? Amount,
    double? FaceValue = null,
    DateOnly? PeriodStartDate = null,
    double? AnnualRatePercent = null,
    BondCashFlowCurrency Currency = BondCashFlowCurrency.FaceUnit);
