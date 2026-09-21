namespace Popo.Core.Contracts;

public sealed class CbondSecurityDto
{
    public string Isin { get; init; } = string.Empty;
    public List<CbondRatingDto> Ratings { get; init; } = [];
}

public sealed class CbondRatingDto
{
    public string SourceName { get; init; } = string.Empty;
    public string RatingName { get; init; } = string.Empty;
}

