namespace Popo.Core.Contracts;

public sealed record CbrCurrencyDefinitionDto(
    string CbrCode,
    string CurrencyCode,
    string Name);

public sealed record CbrCurrencyRateDto(
    DateOnly RateDate,
    string CurrencyCode,
    string Name,
    double Nominal,
    double Value,
    double UnitRate);
