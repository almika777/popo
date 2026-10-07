using Popo.Core.Bonds;

namespace Popo.Core.Portfolio.Position;

public sealed class PositionCalculatorFactory
{
    private readonly IPositionCalculator _defaultCalculator = new DefaultPositionCalculator();
    private readonly IPositionCalculator _zoCalculator = new ZoPositionCalculator();
    private readonly IPositionCalculator _floaterCalculator = new FloaterPositionCalculator();

    public IPositionCalculator Create(
        string? faceUnit,
        string? currencyId,
        string? bondType = null,
        string? bondSubType = null)
    {
        if (IsZoCurrencyPair(faceUnit, currencyId))
            return _zoCalculator;

        return IsRubleCurrencyPair(faceUnit, currencyId)
               && BondCouponClassifier.IsFloater(bondType, bondSubType)
            ? _floaterCalculator
            : _defaultCalculator;
    }

    private static bool IsZoCurrencyPair(string? faceUnit, string? currencyId) =>
        faceUnit is not null
        && currencyId is not null
        && (faceUnit.Equals("USD", StringComparison.OrdinalIgnoreCase)
            || faceUnit.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            || faceUnit.Equals("CNY", StringComparison.OrdinalIgnoreCase))
        && (currencyId.Equals("RUB", StringComparison.OrdinalIgnoreCase)
            || currencyId.Equals("SUR", StringComparison.OrdinalIgnoreCase));

    private static bool IsRubleCurrencyPair(string? faceUnit, string? currencyId) =>
        (faceUnit is not null
         && (faceUnit.Equals("RUB", StringComparison.OrdinalIgnoreCase)
             || faceUnit.Equals("SUR", StringComparison.OrdinalIgnoreCase)))
        && (currencyId is not null
            && (currencyId.Equals("RUB", StringComparison.OrdinalIgnoreCase)
                || currencyId.Equals("SUR", StringComparison.OrdinalIgnoreCase)));
}
