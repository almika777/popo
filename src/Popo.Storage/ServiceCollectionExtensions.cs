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
using Popo.Core.Portfolio.Position;
using Popo.Core.Portfolio.Trades;
using Popo.Core.Portfolio.ClosedSessions;
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
        services.AddScoped<IMoexIndexHistoryStore, MoexIndexHistoryStore>();
        services.AddScoped<IPortfolioBondBenchmarkHistoryProvider, PortfolioBondBenchmarkHistoryProvider>();
        services.AddScoped<DailyVolumeStatisticsStore>();
        services.AddScoped<ICashFlowProvider, CashFlowProvider>();
        services.AddScoped<IPortfolioValuationsProvider, PortfolioValuationsProvider>();
        services.AddScoped<IPortfolioCashFlowsProvider, PortfolioCashFlowsProvider>();
        services.AddScoped<IPortfolioMoneyMarketFundsProvider, PortfolioMoneyMarketFundsProvider>();
        services.AddScoped<IPortfolioTradesProvider, PortfolioTradesProvider>();
        services.AddScoped<IPortfolioPositionsProvider, PortfolioPositionsProvider>();
        services.AddScoped<IPortfolioCurrencyRatesProvider, PortfolioCurrencyRatesProvider>();
        services.AddScoped<IClosedPositionSessionSourceProvider, ClosedPositionSessionSourceProvider>();
        services.AddScoped<IClosedPositionSessionsProvider, ClosedPositionSessionsProvider>();
        services.AddScoped<IPortfolioCouponProvider, PortfolioCouponProvider>();
        services.AddScoped<ICashInvestmentRecommendationStore, InvestmentStrategySettingsStore>();
        services.AddScoped<IInitializationStateStore, InitializationStateStore>();
        return services;
    }
}
