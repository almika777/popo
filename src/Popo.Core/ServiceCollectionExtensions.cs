using Microsoft.Extensions.DependencyInjection;
using Popo.Core.Bonds;
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
        services.AddScoped<IPositionRecommendationService, PositionRecommendationService>();
        services.AddSingleton<PortfolioReturnCalculator>();
        return services;
    }
}
