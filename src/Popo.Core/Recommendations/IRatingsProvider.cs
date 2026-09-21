namespace Popo.Core.Recommendations;

public interface IRatingsProvider
{
    Task<Dictionary<string, RatingsDto>> GetBondRatingsAsync(CancellationToken ct);
}