#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace Popo.Core.Contracts.Iss;

public class CurrencyData
{
    public DateTimeOffset Date { get; set; }
    public DateTimeOffset PreviousDate { get; set; }
    public string PreviousURL { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, Currency> Valute { get; set; }
    public class Currency
    {
        public string ID { get; set; } = string.Empty;
        public string NumCode { get; set; } = string.Empty;
        public string CharCode { get; set; } = string.Empty;
        public int Nominal { get; set; }
        public string? Name { get; set; }
        public double Value { get; set; }
        public double Previous { get; set; }
    }
}


