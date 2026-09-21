using Hangfire;
using Microsoft.EntityFrameworkCore;
using Popo.Core.Enums;
using Popo.Core.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Storage;
using Popo.Storage.Entities;

namespace Popo.Jobs.Jobs;

public sealed class BondRatingUpdateJob(ICbondClient httpClient, IDbContextFactory<PopoDbContext> dbContextFactory,
    ILogger<BondRatingUpdateJob> logger,
    IInitializationProgressReporter progressReporter) : JobBase(dbContextFactory, logger, progressReporter), IBondRatingUpdateJob
{
    private static readonly Dictionary<string, Rating> RatingMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AAA"] = Rating.AAA, ["AAA-"] = Rating.AAA_minus, ["AA+"] = Rating.AA_plus, ["AA"] = Rating.AA,
        ["AA-"] = Rating.AA_minus, ["A+"] = Rating.A_plus, ["A"] = Rating.A, ["A-"] = Rating.A_minus,
        ["BBB+"] = Rating.BBB_plus, ["BBB"] = Rating.BBB, ["BBB-"] = Rating.BBB_minus,
        ["BB+"] = Rating.BB_plus, ["BB"] = Rating.BB, ["BB-"] = Rating.BB_minus,
        ["B+"] = Rating.B_plus, ["B"] = Rating.B, ["B-"] = Rating.B_minus,
        ["CC"] = Rating.CC, ["C"] = Rating.C, ["D"] = Rating.D
    };

    [DisableConcurrentExecution(120)]
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var ratings = await httpClient.GetRatingsAsync(ct);
        await ReportProgressAsync(0, ratings.Count, "Сохранение кредитных рейтингов", ct);
        if (ratings.Count == 0) return;
        var now = DateTimeOffset.UtcNow;
        var entities = ratings.Where(x => !string.IsNullOrWhiteSpace(x.Isin))
            .DistinctBy(x => x.Isin.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new BondRatingEntity
            {
                Isin = x.Isin.Trim(),
                Acra = ParseRating(x.Ratings.FirstOrDefault(r => r.SourceName.Equals("Acra", StringComparison.OrdinalIgnoreCase))?.RatingName),
                Expert = ParseRating(x.Ratings.FirstOrDefault(r => r.SourceName.Equals("Expert", StringComparison.OrdinalIgnoreCase))?.RatingName),
                NRA = ParseRating(x.Ratings.FirstOrDefault(r => r.SourceName.Equals("nra", StringComparison.OrdinalIgnoreCase))?.RatingName),
                NKR = ParseRating(x.Ratings.FirstOrDefault(r => r.SourceName.Equals("ncr", StringComparison.OrdinalIgnoreCase))?.RatingName),
                CreatedAt = now, UpdatedAt = now
            }).ToList();
        await using var context = await CreateDbContextAsync(ct);
        context.BondRatings.RemoveRange(context.BondRatings);
        await context.BondRatings.AddRangeAsync(entities, ct);
        await context.SaveChangesAsync(ct);
        await ReportProgressAsync(ratings.Count, ratings.Count, "Сохранение кредитных рейтингов", ct);
    }

    internal static Rating? ParseRating(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.ToUpperInvariant().Replace("(RU.SF)", "").Replace("(RU)", "")
            .Replace(".RU", "").Replace(".SF", "").Replace("|RU|", "");
        if (normalized.StartsWith("RU", StringComparison.Ordinal)) normalized = normalized[2..];
        return RatingMap.GetValueOrDefault(normalized);
    }
}

public interface IBondRatingUpdateJob : IHangfireJob;
