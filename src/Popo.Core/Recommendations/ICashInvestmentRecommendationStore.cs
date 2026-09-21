namespace Popo.Core.Recommendations;

public sealed record InvestmentStrategyPreset(int Id, string Name, InvestmentStrategySettings Settings, bool IsActive);

public interface ICashInvestmentRecommendationStore
{
    Task<InvestmentStrategySettings> GetSettingsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InvestmentStrategyPreset>> GetPresetsAsync(CancellationToken cancellationToken);
    Task<InvestmentStrategyPreset?> GetPresetAsync(int id, CancellationToken cancellationToken);
    Task<InvestmentStrategyPreset> SavePresetAsync(InvestmentStrategyPreset preset, CancellationToken cancellationToken);
    Task<bool> DeletePresetAsync(int id, CancellationToken cancellationToken);
    Task<bool> ActivatePresetAsync(int id, CancellationToken cancellationToken);
    Task<bool> SaveSettingsAsync(InvestmentStrategySettings settings, CancellationToken cancellationToken);
}
