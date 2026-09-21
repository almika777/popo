using Popo.Core.Enums;

namespace Popo.Storage.Entities;

public class BondRatingEntity
{
    public string Isin { get; set; } = null!;

    public Rating? Acra { get; set; }
    public Rating? Expert { get; set; }
    public Rating? NRA { get; set; }
    public Rating? NKR { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

