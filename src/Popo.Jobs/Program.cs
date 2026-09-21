using Hangfire;
using Hangfire.PostgreSql;
using Popo.Core;
using Popo.Core.Common;
using Popo.Jobs;
using Popo.Jobs.Initialization;
using Popo.Jobs.Jobs;
using Popo.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var connectionString = builder.Configuration.GetPopoConnectionString();

builder.Services.AddPopoCore();
builder.Services.AddPopoDataServices(builder.Configuration);
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer(options =>
{
    options.Queues = ["fast"];
    options.WorkerCount = 1;
});
builder.Services.AddHostedService<InitializationBootstrapService>();
builder.Services.AddHangfireServer(options =>
{
    options.Queues = ["long"];
    options.WorkerCount = 1;
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new DockerLocalRequestsOnlyAuthorizationFilter()]
});

RecurringJobsScheduler.RemoveAll();

app.Run();
