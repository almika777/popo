namespace Popo.Core.Statistics;

public sealed record BondDailyPricePoint(
    string SecId,
    string BoardId,
    string ShortName,
    double Price,
    double? NumTrades = null,
    double? Value = null,
    string? CurrencyId = null,
    BondIssuerCategory? IssuerCategory = null,
    int? EmitterId = null);

public sealed record BondDailySecurityMetadata(string ShortName, BondIssuerCategory? IssuerCategory, int? EmitterId = null);

public sealed record BondDailyChange(
    string SecId,
    string BoardId,
    string ShortName,
    double PreviousPrice,
    double CurrentPrice,
    double ChangePoints,
    double ChangePercent,
    int? EmitterId = null);

public sealed record BondDailyTurnover(
    string SecId,
    string BoardId,
    string ShortName,
    double TurnoverRub,
    int? EmitterId = null);

public sealed record BondDailyChanges(
    IReadOnlyList<BondDailyChange> Declines,
    IReadOnlyList<BondDailyChange> Increases);

public sealed record BondDailyStatisticsResult(
    DateOnly Date,
    DateOnly? ComparedWithDate,
    IReadOnlyList<BondDailyChange> Declines,
    IReadOnlyList<BondDailyChange> Increases,
    IReadOnlyList<BondDailyTurnover> Turnovers);

public interface IBondDailyPriceHistoryProvider
{
    Task<DateOnly?> GetLatestTradingDateBeforeAsync(DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<BondDailyPricePoint>> GetClosingPricesAsync(DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, BondDailySecurityMetadata>> GetSecurityMetadataAsync(
        IReadOnlyCollection<string> secIds,
        CancellationToken cancellationToken);
}
