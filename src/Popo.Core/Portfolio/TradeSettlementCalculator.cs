namespace Popo.Core.Portfolio;

public sealed record TradeSettlement(
    double GrossAmount,
    double CommissionAmount,
    double NetAmount);

public static class TradeSettlementCalculator
{
    public static TradeSettlement Calculate(
        TradeSide side,
        double quantity,
        double price,
        double accruedInterest,
        double commissionPercent)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentException("Укажите корректный тип сделки.", nameof(side));
        }

        if (!double.IsFinite(quantity) || quantity <= 0 || quantity != Math.Truncate(quantity))
        {
            throw new ArgumentException("Количество должно быть положительным целым числом.", nameof(quantity));
        }

        if (!double.IsFinite(price) || price <= 0)
        {
            throw new ArgumentException("Цена должна быть неотрицательной.", nameof(price));
        }

        if (!double.IsFinite(accruedInterest) || accruedInterest < 0)
        {
            throw new ArgumentException("НКД должен быть неотрицательным.", nameof(accruedInterest));
        }

        if (!double.IsFinite(commissionPercent) || commissionPercent < 0)
        {
            throw new ArgumentException("Комиссия должна быть неотрицательной.", nameof(commissionPercent));
        }

        var grossAmount = quantity * (price + accruedInterest);
        var commissionAmount = grossAmount * commissionPercent / 100;
        var netAmount = side == TradeSide.Buy
            ? grossAmount + commissionAmount
            : grossAmount - commissionAmount;

        return new TradeSettlement(grossAmount, commissionAmount, netAmount);
    }
}
