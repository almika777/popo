using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using Popo.Storage;
using Testcontainers.PostgreSql;

namespace Popo.Storage.Tests;

[SetUpFixture]
public sealed class PostgresDatabaseSetUpFixture
{
    [OneTimeSetUp]
    public Task SetUp() => TestDatabase.StartAsync();

    [OneTimeTearDown]
    public Task TearDown() => TestDatabase.DisposeAsync().AsTask();
}

internal static class TestDatabase
{
    private static PostgreSqlContainer _postgres = null!;

    public static IDbContextFactory<PopoDbContext> ContextFactory { get; private set; } = null!;

    public static async Task<IsolatedTestDatabase> CreateIsolatedAsync()
    {
        var schema = $"test_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<PopoDbContext>()
            .UseNpgsql($"{_postgres.GetConnectionString()};Search Path={schema}")
            .Options;

        await using var context = new PopoDbContext(options);
        await ExecuteSchemaCommandAsync(context, $"CREATE SCHEMA \"{schema}\"");
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();

        return new IsolatedTestDatabase(schema, options);
    }

    public static async Task<IsolatedTestDatabase> CreateMigratedAsync()
    {
        var schema = $"migration_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<PopoDbContext>()
            .UseNpgsql($"{_postgres.GetConnectionString()};Search Path={schema}")
            .Options;

        await using var context = new PopoDbContext(options);
        await ExecuteSchemaCommandAsync(context, $"CREATE SCHEMA \"{schema}\"");
        await context.Database.MigrateAsync();

        return new IsolatedTestDatabase(schema, options);
    }

    public static async Task StartAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PopoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        ContextFactory = new TestDbContextFactory(options);

        await using var context = await ContextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public static ValueTask DisposeAsync()
        => _postgres is null ? ValueTask.CompletedTask : _postgres.DisposeAsync();

    internal static async Task ExecuteSchemaCommandAsync(PopoDbContext context, string commandText)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<PopoDbContext> options)
        : IDbContextFactory<PopoDbContext>
    {
        public PopoDbContext CreateDbContext() => new(options);

        public Task<PopoDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PopoDbContext(options));
    }
}

internal sealed class IsolatedTestDatabase(
    string schema,
    DbContextOptions<PopoDbContext> options) : IAsyncDisposable
{
    public IDbContextFactory<PopoDbContext> ContextFactory { get; } = new TestDbContextFactory(options);

    public async ValueTask DisposeAsync()
    {
        await using var context = new PopoDbContext(options);
        await TestDatabase.ExecuteSchemaCommandAsync(context, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
    }

    private sealed class TestDbContextFactory(DbContextOptions<PopoDbContext> contextOptions)
        : IDbContextFactory<PopoDbContext>
    {
        public PopoDbContext CreateDbContext() => new(contextOptions);

        public Task<PopoDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PopoDbContext(contextOptions));
    }
}
