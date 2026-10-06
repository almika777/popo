namespace Popo.Core.Portfolio.Position;

public sealed class PositionCalculatorFactory
{
    private readonly IPositionCalculator _defaultCalculator = new DefaultPositionCalculator();
    private readonly IPositionCalculator _zoCalculator = new ZoPositionCalculator();

    public IPositionCalculator Create(string? faceUnit, string? currencyId) =>
        IsZoCurrencyPair(faceUnit, currencyId) ? _zoCalculator : _defaultCalculator;

    private static bool IsZoCurrencyPair(string? faceUnit, string? currencyId) =>
        faceUnit is not null
        && currencyId is not null
        && (faceUnit.Equals("USD", StringComparison.OrdinalIgnoreCase)
            || faceUnit.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            || faceUnit.Equals("CNY", StringComparison.OrdinalIgnoreCase))
        && (currencyId.Equals("RUB", StringComparison.OrdinalIgnoreCase)
            || currencyId.Equals("SUR", StringComparison.OrdinalIgnoreCase));
}
