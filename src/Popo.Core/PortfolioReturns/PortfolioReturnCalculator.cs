namespace Popo.Core.PortfolioReturns;

public sealed class PortfolioReturnCalculator
{
    public PortfolioReturnResult Calculate(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<PortfolioValuationInput> valuations,
        IReadOnlyCollection<PortfolioCashFlowInput> cashFlows)
    {
        if (to <= from)
        {
            throw new ArgumentException("Конечная дата должна быть позже начальной.", nameof(to));
        }

        var start = FindValuation(from, valuations);
        var end = FindValuation(to, valuations);
        ValidateValue(start.TotalValue, nameof(valuations));
        ValidateValue(end.TotalValue, nameof(valuations));

        var periodFlows = cashFlows
            .Where(flow => flow.Date > from && flow.Date <= to)
            .GroupBy(flow => flow.Date)
            .OrderBy(group => group.Key)
            .ToArray();
        ValidateFlows(periodFlows.SelectMany(group => group));

        var missingDates = periodFlows
            .Where(group => FindPreFlowValuation(group.Key, valuations) is null)
            .Select(group => group.Key.AddDays(-1))
            .ToArray();
        if (missingDates.Length > 0)
        {
            throw new MissingPortfolioValuationsException(missingDates);
        }

        var multiplier = 1d;
        var capitalAfterPreviousFlow = start.TotalValue;
        foreach (var dateFlows in periodFlows)
        {
            var beforeFlow = FindPreFlowValuation(dateFlows.Key, valuations)
                ?? throw new MissingPortfolioValuationsException([dateFlows.Key.AddDays(-1)]);
            multiplier *= beforeFlow.TotalValue / capitalAfterPreviousFlow;

            var summary = CashFlowSummary.From(dateFlows);
            capitalAfterPreviousFlow = beforeFlow.TotalValue
                + summary.Deposits
                - summary.Withdrawals
                - summary.Taxes;
            ValidateCapital(capitalAfterPreviousFlow);
        }

        multiplier *= end.TotalValue / capitalAfterPreviousFlow;

        var periodReturn = multiplier - 1;
        if (!double.IsFinite(periodReturn) || periodReturn <= -1)
        {
            throw new InvalidOperationException("Невозможно рассчитать годовую доходность для указанных данных.");
        }

        var deposits = periodFlows.SelectMany(group => group)
            .Where(flow => flow.Type == PortfolioCashFlowType.Deposit)
            .Sum(flow => flow.Amount);
        var withdrawals = periodFlows.SelectMany(group => group)
            .Where(flow => flow.Type == PortfolioCashFlowType.Withdrawal)
            .Sum(flow => flow.Amount);
        var taxes = periodFlows.SelectMany(group => group)
            .Where(flow => flow.Type == PortfolioCashFlowType.Tax)
            .Sum(flow => flow.Amount);
        var absoluteReturn = end.TotalValue - start.TotalValue - deposits + withdrawals + taxes;
        var periodDays = to.DayNumber - from.DayNumber;
        var annualizedReturn = Math.Pow(1 + periodReturn, 365d / periodDays) - 1;

        return new PortfolioReturnResult(
            from,
            to,
            start.TotalValue,
            end.TotalValue,
            deposits,
            withdrawals,
            taxes,
            absoluteReturn,
            periodReturn,
            annualizedReturn,
            periodDays);
    }

    private static PortfolioValuationInput FindValuation(
        DateOnly date,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        var valuation = FindOptionalValuation(date, valuations);
        return valuation ?? throw new InvalidOperationException(
            $"Укажите оценку портфеля на дату {date:dd.MM.yyyy}.");
    }

    private static PortfolioValuationInput? FindOptionalValuation(
        DateOnly date,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        var matching = valuations.Where(valuation => valuation.Date == date).ToArray();
        return matching.Length switch
        {
            0 => null,
            1 => matching[0],
            _ => throw new InvalidOperationException($"Для даты {date:dd.MM.yyyy} допускается только одна оценка портфеля.")
        };
    }

    private static PortfolioValuationInput? FindPreFlowValuation(
        DateOnly flowDate,
        IReadOnlyCollection<PortfolioValuationInput> valuations)
    {
        return FindOptionalValuation(flowDate.AddDays(-1), valuations);
    }

    private static void ValidateFlows(IEnumerable<PortfolioCashFlowInput> flows)
    {
        foreach (var flow in flows)
        {
            if (!double.IsFinite(flow.Amount) || flow.Amount <= 0)
            {
                throw new ArgumentException("Сумма денежной операции должна быть положительной.", nameof(flows));
            }
        }
    }

    private static void ValidateValue(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentException("Стоимость портфеля должна быть неотрицательной.", parameterName);
        }
    }

    private static void ValidateCapital(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new InvalidOperationException("Капитал после денежной операции должен быть положительным.");
        }
    }

    private sealed class CashFlowSummary
    {
        public double Deposits { get; private set; }
        public double Withdrawals { get; private set; }
        public double Taxes { get; private set; }

        public static CashFlowSummary From(IEnumerable<PortfolioCashFlowInput> flows)
        {
            var summary = new CashFlowSummary();
            foreach (var flow in flows)
            {
                switch (flow.Type)
                {
                    case PortfolioCashFlowType.Deposit:
                        summary.Deposits += flow.Amount;
                        break;
                    case PortfolioCashFlowType.Withdrawal:
                        summary.Withdrawals += flow.Amount;
                        break;
                    case PortfolioCashFlowType.Tax:
                        summary.Taxes += flow.Amount;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(flow));
                }
            }

            return summary;
        }
    }
}
