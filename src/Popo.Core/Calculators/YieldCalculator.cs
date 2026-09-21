using Popo.Core.Recommendations;

namespace Popo.Core.Calculators;

public static class YieldCalculator
{
    private const double DaysInYear = 365.0;
    private const double ResidualTolerance = 1e-10;
    private const double RootTolerance = 1e-14;
    private const int MaxBracketExpansions = 20;
    private const int MaxIterations = 200;

    public static double? TryCalculate(
        DateOnly settlementDate,
        double dirtyPrice,
        IReadOnlyCollection<CashFlow> flows)
    {
        if (!double.IsFinite(dirtyPrice) || dirtyPrice <= 0d)
            return null;

        var futureFlows = flows
            .Where(x => x.Date > settlementDate)
            .OrderBy(x => x.Date)
            .ToArray();

        if (futureFlows.Length == 0 ||
            futureFlows.Any(x => !double.IsFinite(x.ValueInRub) || x.ValueInRub < 0d) ||
            futureFlows.All(x => x.ValueInRub == 0d))
        {
            return null;
        }

        double PresentValue(double logRate)
        {
            double sum = 0;
            double compensation = 0;

            foreach (var flow in futureFlows)
            {
                var years = (flow.Date.DayNumber - settlementDate.DayNumber) / DaysInYear;
                var exponent = -logRate * years;
                if (exponent > 709d)
                    return double.PositiveInfinity;

                var discountedValue = exponent < -745d
                    ? 0d
                    : flow.ValueInRub * Math.Exp(exponent);
                var correctedValue = discountedValue - compensation;
                var nextSum = sum + correctedValue;
                compensation = (nextSum - sum) - correctedValue;
                sum = nextSum;
            }

            return sum;
        }

        double Npv(double logRate) => PresentValue(logRate) - dirtyPrice;

        var atZero = Npv(0d);
        if (atZero == 0d)
            return 0d;

        var lower = 0d;
        var upper = 0d;
        var lowerNpv = atZero;
        var upperNpv = atZero;
        var step = 1d;

        for (var i = 0; i < MaxBracketExpansions && lowerNpv * upperNpv > 0d; i++)
        {
            if (atZero > 0d)
            {
                upper = step;
                upperNpv = Npv(upper);
            }
            else
            {
                lower = -step;
                lowerNpv = Npv(lower);
            }

            step *= 2d;
        }

        if (lowerNpv < 0d || upperNpv > 0d)
            return null;

        var root = 0d;
        for (var i = 0; i < MaxIterations; i++)
        {
            root = lower + (upper - lower) / 2d;
            var rootNpv = Npv(root);

            if (rootNpv == 0d || upper - lower <= RootTolerance)
            {
                break;
            }

            if (rootNpv > 0d)
            {
                lower = root;
                lowerNpv = rootNpv;
            }
            else
            {
                upper = root;
                upperNpv = rootNpv;
            }
        }

        var annualRate = Expm1(root);
        var presentValue = PresentValue(root);
        if (!double.IsFinite(annualRate) ||
            !double.IsFinite(presentValue) ||
            Math.Abs(presentValue - dirtyPrice) / dirtyPrice > ResidualTolerance)
        {
            return null;
        }

        return annualRate * 100d;
    }

    private static double Expm1(double value)
    {
        if (Math.Abs(value) > 1e-5d)
            return Math.Exp(value) - 1d;

        var sum = value;
        var term = value;
        for (var divisor = 2d; divisor <= 12d; divisor++)
        {
            term *= value / divisor;
            sum += term;
        }

        return sum;
    }
}
