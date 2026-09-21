namespace Popo.Core.PortfolioReturns;

public enum PortfolioCashFlowType
{
    Deposit,
    Withdrawal,
    Tax
}

public sealed record PortfolioValuationInput(
    DateOnly Date,
    double TotalValue);

public sealed record PortfolioCashFlowInput(
    DateOnly Date,
    PortfolioCashFlowType Type,
    double Amount);

public sealed record PortfolioValuationRecord(
    Guid Id,
    DateOnly Date,
    double TotalValue,
    string Comment);

public sealed record PortfolioCashFlowRecord(
    Guid Id,
    DateOnly Date,
    PortfolioCashFlowType Type,
    double Amount,
    string Comment);

public sealed record PortfolioReturnResult(
    DateOnly From,
    DateOnly To,
    double StartValue,
    double EndValue,
    double Deposits,
    double Withdrawals,
    double Taxes,
    double AbsoluteReturn,
    double PeriodReturn,
    double AnnualizedReturn,
    int PeriodDays);

public sealed class MissingPortfolioValuationsException(IReadOnlyList<DateOnly> requiredDates)
    : InvalidOperationException("Для каждой даты денежной операции нужна оценка портфеля за предыдущий календарный день.")
{
    public IReadOnlyList<DateOnly> RequiredDates { get; } = requiredDates;
}
