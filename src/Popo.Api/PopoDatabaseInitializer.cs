using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Popo.Storage;

namespace Popo.Api;

internal static class PopoDatabaseInitializer 
{
    public static void Initialize(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PopoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            context.Database.Migrate();
            logger.LogInformation("Миграции базы Popo успешно применены.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "При применении миграций базы Popo произошла ошибка.");
            throw;
        }
    }
}
