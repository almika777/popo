using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Popo.Api.Filters;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Api.Services.BrokerReports;
using Popo.Api.Services.Position;
using Popo.Api.Services.Trades;
using Popo.Api.Validators;
using Popo.Core;
using Popo.HttpClients;
using Popo.Storage;

namespace Popo.Api;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddPopoApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddControllers(options =>
            {
                options.Filters.Add<FluentValidationActionFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult("Проверьте введённые данные.");
        });
        services.AddOpenApi();
        services.AddCors(options => options.AddPolicy("LocalFrontend", policy => policy
            .WithOrigins(
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "http://localhost:7955",
                "http://127.0.0.1:7955")
            .AllowAnyHeader()
            .AllowAnyMethod()));
        services.AddPopoCore();
        services.AddPopoHttpClients();
        services.AddPopoStorage(configuration.GetPopoConnectionString());
        services.AddScoped<IValidator<BondSearchRequest>, BondSearchRequestValidator>();
        services.AddScoped<IValidator<BrokerReportPreviewRequest>, BrokerReportPreviewRequestValidator>();
        services.AddScoped<IValidator<BrokerReportImportRequest>, BrokerReportImportRequestValidator>();
        services.AddScoped<InvestmentStrategySettingsRequestValidator>();
        services.AddScoped<IValidator<InvestmentStrategySettingsRequest>>(provider =>
            provider.GetRequiredService<InvestmentStrategySettingsRequestValidator>());
        services.AddScoped<IValidator<InvestmentStrategyPresetRequest>, InvestmentStrategyPresetRequestValidator>();
        services.AddScoped<IValidator<UpsertCashSnapshotRequest>, UpsertCashSnapshotRequestValidator>();
        services.AddScoped<IValidator<UpsertMoneyMarketFundRequest>, UpsertMoneyMarketFundRequestValidator>();
        services.AddScoped<IValidator<AddMoneyMarketFundOperationRequest>, AddMoneyMarketFundOperationRequestValidator>();
        services.AddScoped<IValidator<UpsertPortfolioValuationRequest>, UpsertPortfolioValuationRequestValidator>();
        services.AddScoped<IValidator<UpsertPortfolioCashFlowRequest>, UpsertPortfolioCashFlowRequestValidator>();
        services.AddScoped<IValidator<PortfolioReturnQuery>, PortfolioReturnQueryValidator>();
        services.AddScoped<IValidator<UpsertPortfolioTradeRequest>, UpsertPortfolioTradeRequestValidator>();
        services.AddScoped<IPortfolioPositionsService, PortfolioPositionsService>();
        services.AddScoped<PortfolioTradesService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<BrokerReportFaceValueResolver>();
        services.AddScoped<IBrokerReportPdfParser, TBankBrokerReportPdfParser>();
        services.AddScoped<BrokerReportImportService>();
        services.AddScoped<CashPageService>();
        services.AddScoped<PortfolioPageService>();
        services.AddScoped<CashRecommendationsPageService>();

        return services;
    }
}
