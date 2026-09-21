using Popo.HttpClients;
using Popo.Jobs.Initialization;
using Popo.Jobs.Jobs.Mappings;
using Popo.Storage;

namespace Popo.Jobs.Jobs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPopoDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        MapConfig.Register();
        services.AddPopoHttpClients();
        services.AddPopoStorage(configuration.GetPopoConnectionString());
        services.AddScoped<IMoexBondUpdateJob, MoexBondUpdateJob>();
        services.AddScoped<IMoexBondSecuritiesUpdateJob, MoexBondSecuritiesUpdateJob>();
        services.AddScoped<IMoexHistoryPricesUpdateJob, MoexHistoryPricesUpdateJob>();
        services.AddScoped<IMoexAmortsAndCouponsUpdateJob, MoexAmortsAndCouponsUpdateJob>();
        services.AddScoped<ISecUpdateJob, SecUpdateJob>();
        services.AddScoped<IMoexEmitentUpdateJob, MoexEmitentUpdateJob>();
        services.AddScoped<IBondRatingUpdateJob, BondRatingUpdateJob>();
        services.AddScoped<ICbrCurrencyRatesUpdateJob, CbrCurrencyRatesUpdateJob>();
        services.AddScoped<IPositionRecommendationJob, PositionRecommendationJob>();
        services.AddScoped<InitializationExecutionContext>();
        services.AddScoped<IInitializationProgressReporter, InitializationProgressReporter>();
        return services;
    }
}
