using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.Recommendations;

namespace Popo.Api.Services;

public sealed class CashRecommendationSettingsService(ICashInvestmentRecommendationStore store)
{
    public Task<InvestmentStrategySettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        store.GetSettingsAsync(cancellationToken);

    public Task<IReadOnlyList<InvestmentStrategyPreset>> GetPresetsAsync(CancellationToken cancellationToken) =>
        store.GetPresetsAsync(cancellationToken);

    public Task<InvestmentStrategyPreset> CreatePresetAsync(
        InvestmentStrategyPresetRequest request,
        CancellationToken cancellationToken) =>
        store.SavePresetAsync(
            new InvestmentStrategyPreset(0, request.Name, request.Settings.ToDomain(), false), cancellationToken);

    public async Task<InvestmentStrategyPreset> UpdatePresetAsync(
        int id,
        InvestmentStrategyPresetRequest request,
        CancellationToken cancellationToken)
    {
        var current = await store.GetPresetAsync(id, cancellationToken)
            ?? throw new ApplicationResourceNotFoundException("Пресет не найден.");
        return await store.SavePresetAsync(
            current with { Name = request.Name, Settings = request.Settings.ToDomain() }, cancellationToken);
    }

    public async Task DeletePresetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await store.DeletePresetAsync(id, cancellationToken))
            throw new ApplicationResourceNotFoundException("Пресет не найден.");
    }

    public async Task ActivatePresetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await store.ActivatePresetAsync(id, cancellationToken))
            throw new ApplicationResourceNotFoundException("Пресет не найден.");
    }

    public async Task<InvestmentStrategySettings> UpdateSettingsAsync(
        InvestmentStrategySettings settings,
        CancellationToken cancellationToken)
    {
        if (!await store.SaveSettingsAsync(settings, cancellationToken))
            throw new ApplicationConflictException("Не удалось сохранить настройки стратегии.");
        return settings;
    }
}
