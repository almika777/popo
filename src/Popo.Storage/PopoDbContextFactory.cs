using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace Popo.Storage;

public sealed class PopoDbContextFactory : IDesignTimeDbContextFactory<PopoDbContext>
{
    public PopoDbContext CreateDbContext(string[] args)
    {
        var connectionString = FirstConfiguredValue(
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
                Environment.GetEnvironmentVariable("POPO_DB_CONNECTION_STRING"),
                Environment.GetEnvironmentVariable("POPO_CONNECTION_STRING"))
            ?? ReadConnectionStringFromSettings()
            ?? throw new InvalidOperationException(
                "Для операций EF Core во время разработки необходимо настроить ConnectionStrings:DefaultConnection или POPO_DB_CONNECTION_STRING.");

        var options = new DbContextOptionsBuilder<PopoDbContext>()
            .UseNpgsql(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new PopoDbContext(options);
    }

    private static string? ReadConnectionStringFromSettings()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var settingsPaths = new[]
        {
            Path.Combine(currentDirectory, "appsettings.Development.json"),
            Path.Combine(currentDirectory, "appsettings.json"),
            Path.Combine(currentDirectory, "src", "Popo.Api", "appsettings.Development.json"),
            Path.Combine(currentDirectory, "src", "Popo.Api", "appsettings.json"),
            Path.Combine(currentDirectory, "src", "Popo.Jobs", "appsettings.Development.json"),
            Path.Combine(currentDirectory, "src", "Popo.Jobs", "appsettings.json")
        };

        foreach (var settingsPath in settingsPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(settingsPath))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                || !connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
            {
                continue;
            }

            var value = defaultConnection.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? FirstConfiguredValue(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
