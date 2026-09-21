using Popo.Core.Enums;
using Popo.Core.Recommendations;

namespace Popo.Storage.Entities;

public sealed class InvestmentStrategySettingsEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public Rating MinimumRating { get; set; }

    public int? MinimumMaturityDays { get; set; }

    public int? MaximumMaturityDays { get; set; }

    public double MinimumMedianDailyVolume { get; set; }

    public int OfferWindowDays { get; set; }

    public double MaximumYtm { get; set; }

    public double MinimumYtm { get; set; }

    public BondInstrumentType InstrumentType { get; set; }

    public string? FaceUnit { get; set; }

    public string? CurrencyId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
