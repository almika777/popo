using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Popo.Api;
using Popo.Api.Filters;
using Popo.Api.Services;
using Popo.Core;
using Scalar.AspNetCore;
using FluentValidation;
using Popo.Api.Models;
using Popo.HttpClients;
using Popo.Storage;

if (args is ["--healthcheck"])
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await client.GetAsync("http://127.0.0.1:8080/health");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (OperationCanceledException)
    {
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services
    .AddControllers(options => options.Filters.Add<FluentValidationActionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult("Проверьте введённые данные."));
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy => policy
    .WithOrigins(
        "http://localhost:3000",
        "http://127.0.0.1:3000",
        "http://localhost:7955",
        "http://127.0.0.1:7955")
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddPopoCore();
builder.Services.AddPopoHttpClients();
builder.Services.AddPopoStorage(builder.Configuration.GetPopoConnectionString());
builder.Services.AddScoped<IValidator<InvestmentStrategySettingsRequest>, InvestmentStrategySettingsRequestValidator>();
builder.Services.AddScoped<IPortfolioPositionsService, PortfolioPositionsService>();
builder.Services.AddScoped<CashPageService>();
builder.Services.AddScoped<PortfolioPageService>();
builder.Services.AddScoped<CashRecommendationsPageService>();


var app = builder.Build();

PopoDatabaseInitializer.Initialize(app);

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    if (exception is not null)
    {
        logger.LogError(exception, "Необработанная ошибка API.");
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "text/plain; charset=utf-8";
    await context.Response.WriteAsync("Внутренняя ошибка сервера. Попробуйте ещё раз.");
}));

app.UseCors("LocalFrontend");
app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
