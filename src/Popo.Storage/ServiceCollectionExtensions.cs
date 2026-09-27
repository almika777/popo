using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Popo.Core;
using Popo.Core.Bonds;
using Popo.Core.PortfolioReturns;
using Popo.Core.Portfolio;
using Popo.Storage.Providers.Bonds;
using Popo.Storage.Providers.PortfolioReturns;
using Popo.Storage.Providers.Portfolio;
using Popo.Core.Recommendations;
using Popo.Core.Initialization;
using Popo.Storage.Providers;
using Popo.Storage.Providers.Ratings;

namespace Popo.Storage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPopoStorage(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<PopoDbContext>(options => 
            options.UseNpgsql(connectionString).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddScoped<IBondsProvider, BondsProvider>();
        services.AddScoped<IRatingsProvider, RatingsProvider>();
        services.AddScoped<IHistoryProvider, HistoryProvider>();
        services.AddScoped<DailyVolumeStatisticsStore>();
        services.AddScoped<ICashFlowProvider, CashFlowProvider>();
        services.AddScoped<IPortfolioReturnInputsProvider, PortfolioReturnInputsProvider>();
        services.AddScoped<IPortfolioLedgerProvider, PortfolioLedgerProvider>();
        services.AddScoped<IBrokerReportImportProvider, BrokerReportImportProvider>();
        services.AddScoped<IPortfolioCouponProvider, PortfolioCouponProvider>();
        services.AddScoped<ICashInvestmentRecommendationStore, InvestmentStrategySettingsStore>();
        services.AddScoped<IPositionRecommendationStore, PositionRecommendationStore>();
        services.AddScoped<IInitializationStateStore, InitializationStateStore>();
        return services;
    }
}
