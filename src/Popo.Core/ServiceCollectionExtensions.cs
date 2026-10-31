using Microsoft.Extensions.DependencyInjection;
using Popo.Core.Bonds;
using Popo.Core.Portfolio.ClosedSessions;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;
using Popo.Core.Recommendations;

namespace Popo.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPopoCore(this IServiceCollection services)
    {
        services.AddScoped<IBondsService, BondsService>();
        services.AddScoped<ICashRecommendationService, CashRecommendationService>();
        services.AddScoped<IBondAssessmentService, BondAssessmentService>();
        services.AddSingleton<PortfolioReturnCalculator>();
        services.AddSingleton<PortfolioBenchmarkCalculator>();
        services.AddSingleton<PortfolioCurrencyAnalyticsCalculator>();
        services.AddScoped<ClosedPositionSessionsService>();
        return services;
    }
}
