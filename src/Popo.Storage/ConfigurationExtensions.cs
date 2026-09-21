using Microsoft.Extensions.Configuration;

namespace Popo.Storage;

public static class ConfigurationExtensions
{
    public static string GetPopoConnectionString(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var environmentConnectionString = configuration["POPO_DB_CONNECTION_STRING"];
        return !string.IsNullOrWhiteSpace(environmentConnectionString)
            ? environmentConnectionString
            : configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Необходимо настроить ConnectionStrings:DefaultConnection или POPO_DB_CONNECTION_STRING.");
    }
}
