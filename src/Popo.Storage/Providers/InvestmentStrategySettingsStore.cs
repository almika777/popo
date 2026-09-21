using Microsoft.EntityFrameworkCore;
using Popo.Core.Enums;
using Popo.Core.Recommendations;
using Popo.Storage.Entities;

namespace Popo.Storage.Providers;

public sealed class InvestmentStrategySettingsStore(IDbContextFactory<PopoDbContext> dbContextFactory)
    : ICashInvestmentRecommendationStore
{
    public async Task<InvestmentStrategySettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.InvestmentStrategySettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IsActive, cancellationToken);
        return entity is null ? InvestmentStrategySettings.Defaults : ToDomain(entity);
    }

    public async Task<IReadOnlyList<InvestmentStrategyPreset>> GetPresetsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.InvestmentStrategySettings
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new InvestmentStrategyPreset(x.Id, x.Name, ToDomain(x), x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvestmentStrategyPreset?> GetPresetAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.InvestmentStrategySettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null
            ? null
            : new InvestmentStrategyPreset(entity.Id, entity.Name, ToDomain(entity), entity.IsActive);
    }

    public async Task<InvestmentStrategyPreset> SavePresetAsync(InvestmentStrategyPreset preset,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = preset.Id == 0
            ? new InvestmentStrategySettingsEntity
                { Id = (await db.InvestmentStrategySettings.MaxAsync(x => (int?)x.Id, cancellationToken) ?? 0) + 1 }
            : await db.InvestmentStrategySettings.AsTracking().SingleAsync(x => x.Id == preset.Id, cancellationToken);
        if (preset.Id == 0) db.InvestmentStrategySettings.Add(entity);
        entity.Name = preset.Name;
        entity.IsActive = preset.IsActive;
        Apply(entity, preset.Settings);
        await db.SaveChangesAsync(cancellationToken);
        return new InvestmentStrategyPreset(entity.Id, entity.Name, ToDomain(entity), entity.IsActive);
    }

    public async Task<bool> DeletePresetAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.InvestmentStrategySettings.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;
        db.InvestmentStrategySettings.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        if (entity.IsActive)
        {
            var replacement = await db.InvestmentStrategySettings.OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (replacement is not null)
            {
                replacement.IsActive = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return true;
    }

    public async Task<bool> ActivatePresetAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.InvestmentStrategySettings.AnyAsync(x => x.Id == id, cancellationToken)) return false;
        await db.InvestmentStrategySettings.ExecuteUpdateAsync(x => x.SetProperty(p => p.IsActive, p => p.Id == id),
            cancellationToken);
        return true;
    }

    public async Task<bool> SaveSettingsAsync(InvestmentStrategySettings settings, CancellationToken cancellationToken)
    {
        var active = (await GetPresetsAsync(cancellationToken)).SingleOrDefault(x => x.IsActive);
        if (active is null) return false;
        await SavePresetAsync(active with { Settings = settings }, cancellationToken);
        return true;
    }

    private static void Apply(InvestmentStrategySettingsEntity entity, InvestmentStrategySettings settings)
    {
        entity.MinimumRating = (Rating)settings.MinimumRating;
        entity.MinimumMaturityDays = settings.MinimumMaturityDays;
        entity.MaximumMaturityDays = settings.MaximumMaturityDays;
        entity.MinimumMedianDailyVolume = settings.MinimumMedianDailyVolume;
        entity.OfferWindowDays = settings.OfferWindowDays;
        entity.MaximumYtm = settings.MaximumYtm;
        entity.MinimumYtm = settings.MinimumYtm;
        entity.InstrumentType = settings.InstrumentType;
        entity.FaceUnit = settings.FaceUnit;
        entity.CurrencyId = settings.CurrencyId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal static InvestmentStrategySettings ToDomain(InvestmentStrategySettingsEntity entity) => new(
        (CreditRating)entity.MinimumRating, entity.MinimumMaturityDays, entity.MaximumMaturityDays,
        entity.MinimumMedianDailyVolume,
        entity.OfferWindowDays,
        entity.MaximumYtm, entity.MinimumYtm, entity.InstrumentType, entity.FaceUnit, entity.CurrencyId);
}