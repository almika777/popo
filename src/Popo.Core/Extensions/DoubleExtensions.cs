namespace Popo.Core.Extensions;

public static class DoubleExtensions
{
    public static double Round(this double x, int round = 2) => Math.Round(x, round);
}