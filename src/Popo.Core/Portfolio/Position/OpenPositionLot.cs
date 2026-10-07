namespace Popo.Core.Portfolio.Position;

public sealed class OpenPositionLot(
    DateOnly tradeDate,
    double quantity,
    double cleanPrice,
    double actualCleanPrice,
    double commissionPerUnit,
    double paidAccruedInterestPerUnit,
    double? cleanPricePercent)
{
    public DateOnly TradeDate { get; } = tradeDate;
    public double Quantity { get; internal set; } = quantity;
    public double CleanPrice { get; } = cleanPrice;
    public double ActualCleanPrice { get; } = actualCleanPrice;
    public double CommissionPerUnit { get; } = commissionPerUnit;
    public double PaidAccruedInterestPerUnit { get; } = paidAccruedInterestPerUnit;
    public double? CleanPricePercent { get; } = cleanPricePercent;
}
