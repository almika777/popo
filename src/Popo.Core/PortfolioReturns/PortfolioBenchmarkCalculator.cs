using Popo.Core.Contracts.Iss;
using Popo.Core.Portfolio;

namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioBenchmarkCalculator
{
    private readonly PortfolioBenchmarkSeriesCalculator _portfolioCalculator = new();
    private readonly LqdtBenchmarkSeriesCalculator _lqdtCalculator = new();
    private readonly Ru000A10F504BenchmarkSeriesCalculator _indexedBondCalculator = new();

    public PortfolioBenchmarkResult Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows,
        IReadOnlyCollection<MoexIndexHistory> lqdtPriceHistory,
        PortfolioBondBenchmarkHistory? indexedBondHistory = null,
        bool includeLqdt = true,
        bool includeIndexedBond = true)
    {
        ArgumentNullException.ThrowIfNull(valuations);
        ArgumentNullException.ThrowIfNull(cashFlows);
        ArgumentNullException.ThrowIfNull(lqdtPriceHistory);
        if (to <= from)
        {
            throw new ArgumentException("Конечная дата должна быть позже начальной.", nameof(to));
        }

        var lqdt = _lqdtCalculator.Calculate(from, to, lqdtPriceHistory);
        var indexedBondFirstObservationDate = _indexedBondCalculator
            .FindFirstObservationDate(from, to, indexedBondHistory);

        DateOnly? candidateComparisonStartDate = indexedBondFirstObservationDate.HasValue
            ? LaterOf(lqdt.StartPriceDate, indexedBondFirstObservationDate.Value)
            : null;
        candidateComparisonStartDate = (includeLqdt, includeIndexedBond) switch
        {
            (true, true) => candidateComparisonStartDate,
            (true, false) => lqdt.StartPriceDate,
            (false, true) => indexedBondFirstObservationDate,
            _ => null
        };

        var indexedBond = _indexedBondCalculator
            .Calculate(from, to, indexedBondHistory, includeIndexedBond ? candidateComparisonStartDate : null);
        var hasAvailableSelectedIndexes = (!includeLqdt || candidateComparisonStartDate.HasValue) &&
            (!includeIndexedBond || indexedBond.Series is not null);
        var comparisonStartDate = hasAvailableSelectedIndexes && (includeLqdt || includeIndexedBond)
            ? candidateComparisonStartDate
            : null;
        var lqdtSeries = comparisonStartDate.HasValue
            ? RebaseSeries(lqdt.Series, comparisonStartDate.Value)
            : lqdt.Series;
        BenchmarkSeriesCalculation? portfolio = null;
        string? portfolioUnavailableReason = null;
        try
        {
            var calculationStartDate = comparisonStartDate ?? from;
            if (comparisonStartDate.HasValue && !valuations.Any(value => value.Date == calculationStartDate))
            {
                portfolioUnavailableReason =
                    $"Нет оценки портфеля на дату {calculationStartDate:dd.MM.yyyy}. Добавьте оценку портфеля за эту дату, чтобы сравнение было сопоставимым.";
            }
            else
            {
                var periodValuations = GetPeriodValuations(calculationStartDate, to, valuations);
                var startValue = PortfolioBenchmarkMath.FindValuation(calculationStartDate, periodValuations).TotalValue;
                if (startValue <= 0)
                {
                    throw new ArgumentException("Стоимость портфеля на начало периода должна быть больше нуля.", nameof(valuations));
                }

                var periodFlows = cashFlows.Where(flow => flow.Date > calculationStartDate && flow.Date <= to)
                    .OrderBy(flow => flow.Date).ToArray();
                ValidateCashFlows(periodFlows);
                PortfolioBenchmarkMath.EnsureFlowDateValuations(periodFlows, periodValuations);
                portfolio = _portfolioCalculator.Calculate(
                    calculationStartDate, to, periodValuations, periodFlows);
            }
        }
        catch (MissingPortfolioBenchmarkValuationsException exception)
        {
            portfolioUnavailableReason =
                $"Для сравнения добавьте оценку портфеля на конец дня после денежных операций: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.";
        }
        catch (MissingPortfolioValuationsException exception)
        {
            portfolioUnavailableReason =
                $"Для сравнения добавьте оценку портфеля на даты: {string.Join(", ", exception.RequiredDates.Select(date => date.ToString("yyyy-MM-dd")))}.";
        }
        catch (ArgumentException exception)
        {
            portfolioUnavailableReason = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            portfolioUnavailableReason = exception.Message;
        }

        double? excessReturn = portfolio is not null &&
            (comparisonStartDate.HasValue || lqdt.StartPriceDate == from)
            ? portfolio.ProfitPercent - lqdtSeries.ProfitPercent
            : null;

        return new PortfolioBenchmarkResult(
            from, to, portfolio?.ProfitPercent, lqdtSeries.ProfitPercent,
            excessReturn,
            lqdt.StartPriceDate, lqdt.EndPriceDate,
            portfolio?.Points ?? [], lqdtSeries.Points,
            indexedBond.Series?.ProfitPercent,
            indexedBond.StartObservationDate,
            indexedBond.EndObservationDate,
            indexedBond.Series?.Points,
            indexedBond.UnavailableReason,
            indexedBond.ComparisonStartDate,
            portfolio?.ProfitPercent,
            lqdtSeries.ProfitPercent,
            indexedBond.Series?.ProfitPercent,
            portfolioUnavailableReason,
            comparisonStartDate);
    }

    private static DateOnly LaterOf(DateOnly first, DateOnly second) => first >= second ? first : second;

    private static BenchmarkSeriesCalculation RebaseSeries(
        BenchmarkSeriesCalculation series,
        DateOnly startDate)
    {
        var anchor = series.Points.SingleOrDefault(point => point.Date == startDate)
            ?? throw new InvalidPortfolioBenchmarkHistoryException(
                $"В истории LQDT нет расчётной цены на общую дату {startDate:yyyy-MM-dd}.");
        var anchorReturn = anchor.ProfitPercent
            ?? throw new InvalidPortfolioBenchmarkHistoryException("В истории LQDT отсутствует доходность на общую дату сравнения.");
        var anchorValue = 1d + anchorReturn;
        if (!double.IsFinite(anchorValue) || anchorValue <= 0)
        {
            throw new InvalidPortfolioBenchmarkHistoryException("Невозможно пересчитать LQDT от общей даты сравнения.");
        }

        var points = series.Points
            .Where(point => point.Date >= startDate)
            .Select(point =>
            {
                var value = point.ProfitPercent
                    ?? throw new InvalidPortfolioBenchmarkHistoryException("В истории LQDT отсутствует значение доходности.");
                var rebasedReturn = (1d + value) / anchorValue - 1d;
                return point with { ReturnPercent = rebasedReturn, ProfitPercent = rebasedReturn };
            })
            .ToArray();

        if (points.Length == 0)
        {
            throw new InvalidPortfolioBenchmarkHistoryException("В истории LQDT нет точек от общей даты сравнения.");
        }

        return new BenchmarkSeriesCalculation(points[^1].ProfitPercent!.Value, points);
    }

    private static IReadOnlyCollection<PortfolioValuationInput> GetPeriodValuations(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        var duplicate = valuations.GroupBy(value => value.Date).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Для даты {duplicate.Key:dd.MM.yyyy} допускается только одна оценка портфеля.");
        }

        if (valuations.Any(value => !double.IsFinite(value.TotalValue) || value.TotalValue < 0))
        {
            throw new ArgumentException("Стоимость портфеля должна быть неотрицательной.", nameof(valuations));
        }

        var period = valuations.Where(value => value.Date >= from && value.Date <= to).OrderBy(value => value.Date).ToArray();
        _ = PortfolioBenchmarkMath.FindValuation(from, period);
        _ = PortfolioBenchmarkMath.FindValuation(to, period);
        return period;
    }

    private static void ValidateCashFlows(IEnumerable<PortfolioCashFlowInput> flows)
    {
        foreach (var flow in flows)
        {
            if (!double.IsFinite(flow.Amount) || flow.Amount <= 0)
            {
                throw new ArgumentException("Сумма денежной операции должна быть положительной.", nameof(flows));
            }
        }
    }
}
