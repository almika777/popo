using Hangfire;
using Hangfire.PostgreSql;
using Popo.Storage;

namespace Popo.Api.Services.ClosedPositions;

public static class ClosedPositionSessionsRegistration
{
    public static IServiceCollection AddClosedPositionSessionsApi(this IServiceCollection services,
        IConfiguration configuration)
    {
        // API только ставит задачи; их исполняет существующий host Popo.Jobs.
        services.AddHangfire(options => options
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(configuration.GetPopoConnectionString())));
        services.AddScoped<ClosedPositionSessionsPageService>();
        return services;
    }
}
