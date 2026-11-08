using Popo.Core.Bonds;

namespace Popo.Core.Portfolio.Position;

public sealed class PositionCalculatorFactory
{
    private readonly IPositionCalculator _defaultCalculator = new DefaultPositionCalculator();
    private readonly IPositionCalculator _floaterCalculator = new FloaterPositionCalculator();

    public IPositionCalculator Create(string? bondType)
    {
        var category = Classify(bondType);
        return category switch
        {
            PositionBondType.KnownFixedCoupon => _defaultCalculator,
            PositionBondType.KnownFixedCouponShort => _defaultCalculator,
            PositionBondType.UnknownFixedCoupon => _defaultCalculator,
            PositionBondType.UnknownFixedCouponShort => _defaultCalculator,
            PositionBondType.Floating => _floaterCalculator,
            PositionBondType.Floater => _floaterCalculator,
            PositionBondType.Amortized => _defaultCalculator,
            PositionBondType.ForeignCurrency => _defaultCalculator,
            PositionBondType.IndexLinked => _defaultCalculator,
            PositionBondType.Structured => _defaultCalculator,
            PositionBondType.Discount => _defaultCalculator,
            PositionBondType.Convertible => _defaultCalculator,
            _ when BondCouponClassifier.IsFloater(bondType, null) => _floaterCalculator,
            _ => _defaultCalculator
        };
    }

    private static PositionBondType? Classify(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "ОБЛИГАЦИЯ С ФИКСИРОВАННЫМ (ИЗВЕСТНЫМ) КУПОНОМ" => PositionBondType.KnownFixedCoupon,
        "ФИКС С ИЗВЕСТНЫМ КУПОНОМ" => PositionBondType.KnownFixedCouponShort,
        "ОБЛИГАЦИЯ С ФИКСИРОВАННЫМ (НЕИЗВЕСТНЫМ) КУПОНОМ" => PositionBondType.UnknownFixedCoupon,
        "ФИКС С НЕИЗВЕСТНЫМ КУПОНОМ" => PositionBondType.UnknownFixedCouponShort,
        "ОБЛИГАЦИЯ С ПЛАВАЮЩИМ КУПОНОМ" => PositionBondType.Floating,
        "ФЛОАТЕР" => PositionBondType.Floater,
        "АМОРТИЗИРУЕМАЯ ОБЛИГАЦИЯ" or "АМОРТИЗИРУЕМЫЕ ОБЛИГАЦИИ" => PositionBondType.Amortized,
        "ВАЛЮТНАЯ ОБЛИГАЦИЯ" or "ВАЛЮТНЫЕ ОБЛИГАЦИИ" => PositionBondType.ForeignCurrency,
        "ЛИНКЕР/ОБЛИГАЦИЯ С ИНДЕКСИРУЕМЫМ НОМИНАЛОМ" => PositionBondType.IndexLinked,
        "СТРУКТУРНАЯ ОБЛИГАЦИЯ" => PositionBondType.Structured,
        "ДИСКОНТНАЯ ОБЛИГАЦИЯ" => PositionBondType.Discount,
        "КОНВЕРТИРУЕМАЯ ОБЛИГАЦИЯ" => PositionBondType.Convertible,
        _ => null
    };

    private enum PositionBondType
    {
        KnownFixedCoupon,
        KnownFixedCouponShort,
        UnknownFixedCoupon,
        UnknownFixedCouponShort,
        Floating,
        Floater,
        Amortized,
        ForeignCurrency,
        IndexLinked,
        Structured,
        Discount,
        Convertible
    }

}
