using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using FluentValidation;
using NUnit.Framework;
using Popo.Api;
using Popo.Api.Controllers;
using Popo.Api.Models;

namespace Popo.Tests;

public sealed class ApiDependencyRegistrationTests
{
    [Test]
    public void Api_registration_resolves_dependencies_for_every_controller()
    {
        var assembly = typeof(BondsController).Assembly;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = assembly.GetName().Name,
            EnvironmentName = Environments.Development
        });
        const string testConnectionString =
            "Host=127.0.0.1;Database=unused;Username=unused;Password=unused";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["POPO_DB_CONNECTION_STRING"] = testConnectionString,
            ["ConnectionStrings:DefaultConnection"] = testConnectionString
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        builder.Services.AddPopoApiServices(builder.Configuration);

        using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<IOptions<MvcOptions>>()
            .Value.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes, Is.True);

        var controllers = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        Assert.That(controllers, Is.Not.Empty);
        foreach (var controllerType in controllers)
        {
            Assert.DoesNotThrow(() =>
            {
                var controller = ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
                (controller as IDisposable)?.Dispose();
            }, $"{controllerType.Name} must have all constructor dependencies registered.");
        }

        var requestTypes = new[]
        {
            typeof(BondSearchRequest),
            typeof(BrokerReportPreviewRequest),
            typeof(UpsertPortfolioTradeRequest),
            typeof(UpsertCashSnapshotRequest),
            typeof(UpsertMoneyMarketFundRequest),
            typeof(AddMoneyMarketFundOperationRequest),
            typeof(UpsertPortfolioValuationRequest),
            typeof(UpsertPortfolioCashFlowRequest),
            typeof(PortfolioReturnQuery),
            typeof(BrokerReportImportRequest),
            typeof(InvestmentStrategySettingsRequest),
            typeof(InvestmentStrategyPresetRequest)
        };

        foreach (var requestType in requestTypes)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
            Assert.That(scope.ServiceProvider.GetService(validatorType), Is.Not.Null,
                $"{requestType.Name} must have a FluentValidation validator registered.");
        }
    }
}
