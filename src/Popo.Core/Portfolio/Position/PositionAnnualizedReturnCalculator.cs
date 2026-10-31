namespace Popo.Core.Portfolio.Position;

internal static class PositionAnnualizedReturnCalculator
{
    public static PositionAnnualizedReturnBreakdown? Calculate(
        double totalPnl,
        IReadOnlyCollection<OpenPositionLot> lots,
        DateOnly valuationDate,
        DateOnly? latestQuoteDate = null)
    {
        ArgumentNullException.ThrowIfNull(lots);

        if (!double.IsFinite(totalPnl) || lots.Count == 0)
            return null;

        var investedCapitalDays = 0d;
        var lotBreakdowns = new List<PositionAnnualizedLotBreakdown>(lots.Count);
        foreach (var lot in lots)
        {
            if (lot.TradeDate > valuationDate
                || latestQuoteDate.HasValue && lot.TradeDate > latestQuoteDate.Value)
                return null;

            var investedPerUnit = lot.ActualCleanPrice
                                  + lot.PaidAccruedInterestPerUnit
                                  + lot.CommissionPerUnit;
            if (!double.IsFinite(lot.Quantity) || lot.Quantity <= 0
                || !double.IsFinite(investedPerUnit) || investedPerUnit <= 0)
                return null;

            var holdingDays = Math.Max(1, valuationDate.DayNumber - lot.TradeDate.DayNumber);
            var investedCapital = lot.Quantity * investedPerUnit;
            var capitalDays = investedCapital * holdingDays;
            investedCapitalDays += capitalDays;
            lotBreakdowns.Add(new PositionAnnualizedLotBreakdown(
                lot.TradeDate,
                lot.Quantity,
                lot.ActualCleanPrice,
                lot.PaidAccruedInterestPerUnit,
                lot.CommissionPerUnit,
                investedCapital,
                holdingDays,
                capitalDays));
        }

        if (!double.IsFinite(investedCapitalDays) || investedCapitalDays <= 0)
            return null;

        var annualizedReturn = totalPnl * 365 / investedCapitalDays * 100;
        return double.IsFinite(annualizedReturn)
            ? new PositionAnnualizedReturnBreakdown(
                valuationDate,
                totalPnl,
                investedCapitalDays,
                annualizedReturn,
                lotBreakdowns)
            : null;
    }
}

public sealed record PositionAnnualizedReturnBreakdown(
    DateOnly ValuationDate,
    double TotalPnl,
    double InvestedCapitalDays,
    double AnnualizedPercent,
    IReadOnlyList<PositionAnnualizedLotBreakdown> Lots);

public sealed record PositionAnnualizedLotBreakdown(
    DateOnly TradeDate,
    double Quantity,
    double PurchaseCleanPricePerUnit,
    double PaidAccruedInterestPerUnit,
    double CommissionPerUnit,
    double InvestedCapital,
    int HoldingDays,
    double CapitalDays);
