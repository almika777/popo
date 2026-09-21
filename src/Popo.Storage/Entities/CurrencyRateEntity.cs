namespace Popo.Storage.Entities;

public sealed class CurrencyRateEntity
{
    public DateOnly RateDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double Nominal { get; set; }

    public double Value { get; set; }

    public double UnitRate { get; set; }
}
