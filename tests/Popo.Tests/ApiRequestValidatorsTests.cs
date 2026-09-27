using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Popo.Api.Filters;
using Popo.Api.Models;
using Popo.Api.Validators;
using Popo.Core.Portfolio;
using Popo.Core.PortfolioReturns;
using Popo.Core.Recommendations;

namespace Popo.Tests;

public sealed class ApiRequestValidatorsTests
{
    [Test]
    public void Request_wrappers_keep_existing_query_and_form_field_names()
    {
        Assert.That(typeof(BondSearchRequest).GetProperty(nameof(BondSearchRequest.Query))!
            .GetCustomAttributes(typeof(FromQueryAttribute), true)
            .Cast<FromQueryAttribute>().Single().Name, Is.EqualTo("q"));
        Assert.That(typeof(BrokerReportPreviewRequest).GetProperty(nameof(BrokerReportPreviewRequest.File))!
            .GetCustomAttributes(typeof(FromFormAttribute), true)
            .Cast<FromFormAttribute>().Single().Name, Is.EqualTo("file"));
        Assert.That(typeof(PortfolioReturnQuery).GetProperty(nameof(PortfolioReturnQuery.From))!
            .GetCustomAttributes(typeof(FromQueryAttribute), true)
            .Cast<FromQueryAttribute>().Single().Name, Is.EqualTo("from"));
        Assert.That(typeof(PortfolioReturnQuery).GetProperty(nameof(PortfolioReturnQuery.To))!
            .GetCustomAttributes(typeof(FromQueryAttribute), true)
            .Cast<FromQueryAttribute>().Single().Name, Is.EqualTo("to"));
    }

    [Test]
    public async Task Bond_search_requires_three_non_whitespace_characters()
    {
        var validator = new BondSearchRequestValidator();

        var shortResult = await validator.ValidateAsync(new BondSearchRequest { Query = "  ab " });
        var validResult = await validator.ValidateAsync(new BondSearchRequest { Query = "  abc " });

        Assert.That(shortResult.Errors.Single().ErrorMessage, Is.EqualTo("Введите не менее 3 символов для поиска."));
        Assert.That(validResult.IsValid, Is.True);
    }

    [Test]
    public async Task Broker_report_preview_rejects_missing_or_invalid_pdf_signature()
    {
        var validator = new BrokerReportPreviewRequestValidator();
        var missingResult = await validator.ValidateAsync(new BrokerReportPreviewRequest());
        await using var stream = new MemoryStream("not-pdf"u8.ToArray());
        var file = new FormFile(stream, 0, stream.Length, "file", "report.pdf");
        var invalidPdfResult = await validator.ValidateAsync(new BrokerReportPreviewRequest { File = file });

        Assert.That(missingResult.Errors.Single().ErrorMessage,
            Is.EqualTo("Выберите PDF-файл брокерского отчёта."));
        Assert.That(invalidPdfResult.Errors.Single().ErrorMessage,
            Is.EqualTo("Загруженный файл не является корректным PDF-документом."));
    }

    [Test]
    public async Task Ledger_request_validators_reject_invalid_cash_fund_and_trade_inputs()
    {
        var snapshot = await new UpsertCashSnapshotRequestValidator().ValidateAsync(
            new UpsertCashSnapshotRequest(" ", new DateOnly(2026, 1, 1), -1, null));
        var fund = await new UpsertMoneyMarketFundRequestValidator().ValidateAsync(
            new UpsertMoneyMarketFundRequest("OTHER", 1.5, 0));
        var fundOperation = await new AddMoneyMarketFundOperationRequestValidator().ValidateAsync(
            new AddMoneyMarketFundOperationRequest("LQDT", new DateOnly(2026, 1, 1), TradeSide.Buy, 1.5, 10, 0));
        var trade = await new UpsertPortfolioTradeRequestValidator().ValidateAsync(
            new UpsertPortfolioTradeRequest("SBER", "TQBR", "RUB", new DateOnly(2026, 1, 1),
                TradeSide.Buy, 1, 10, 1000, 0, 0));

        Assert.That(snapshot.Errors.Select(error => error.ErrorMessage), Does.Contain("Укажите валюту."));
        Assert.That(fund.Errors.Select(error => error.ErrorMessage), Does.Contain("Поддерживаются только LQDT и TMON."));
        Assert.That(fundOperation.Errors.Select(error => error.ErrorMessage),
            Does.Contain("Проверьте фонд, тип операции, количество, цену и комиссию."));
        Assert.That(trade.IsValid, Is.True);
    }

    [Test]
    public async Task Portfolio_request_validators_reject_negative_value_invalid_flow_and_reversed_period()
    {
        var valuation = await new UpsertPortfolioValuationRequestValidator().ValidateAsync(
            new UpsertPortfolioValuationRequest(new DateOnly(2026, 1, 1), -1, null));
        var cashFlow = await new UpsertPortfolioCashFlowRequestValidator().ValidateAsync(
            new UpsertPortfolioCashFlowRequest(new DateOnly(2026, 1, 1), (PortfolioCashFlowType)99, 10, null));
        var period = await new PortfolioReturnQueryValidator().ValidateAsync(
            new PortfolioReturnQuery { From = new DateOnly(2026, 2, 1), To = new DateOnly(2026, 1, 1) });

        Assert.That(valuation.Errors.Single().ErrorMessage,
            Is.EqualTo("Стоимость портфеля должна быть неотрицательной."));
        Assert.That(cashFlow.Errors.Single().ErrorMessage,
            Is.EqualTo("Тип операции должен быть: пополнение, вывод или налог."));
        Assert.That(period.Errors.Single().ErrorMessage,
            Is.EqualTo("Конечная дата должна быть позже начальной."));
    }

    [Test]
    public async Task Recommendation_preset_validator_validates_nested_settings()
    {
        var settingsValidator = new InvestmentStrategySettingsRequestValidator();
        var invalidSettings = new InvestmentStrategySettingsRequest(
            CreditRating.AA,
            -1,
            10,
            100,
            5);
        var presetValidator = new InvestmentStrategyPresetRequestValidator(settingsValidator);

        var settingsResult = await settingsValidator.ValidateAsync(invalidSettings);
        var presetResult = await presetValidator.ValidateAsync(new InvestmentStrategyPresetRequest("preset", invalidSettings));
        var missingSettingsResult = await presetValidator.ValidateAsync(
            new InvestmentStrategyPresetRequest("preset", null!));
        var missingPresetNameResult = await presetValidator.ValidateAsync(
            new InvestmentStrategyPresetRequest(" ", new InvestmentStrategySettingsRequest(
                CreditRating.AA_minus, 30, 730, 1000, 90)));

        Assert.That(settingsResult.Errors.Single().ErrorMessage,
            Is.EqualTo("Минимальный срок до погашения не может быть отрицательным."));
        Assert.That(presetResult.Errors.Single().ErrorMessage,
            Is.EqualTo("Минимальный срок до погашения не может быть отрицательным."));
        Assert.That(missingSettingsResult.Errors.Single().ErrorMessage, Is.EqualTo("Параметры стратегии не переданы."));
        Assert.That(missingPresetNameResult.Errors.Single().ErrorMessage, Is.EqualTo("Укажите название пресета."));
    }

    [Test]
    public async Task Broker_report_import_validator_rejects_empty_and_invalid_operations()
    {
        var validator = new BrokerReportImportRequestValidator();
        var emptyResult = await validator.ValidateAsync(new BrokerReportImportRequest([]));
        var invalidOperationResult = await validator.ValidateAsync(new BrokerReportImportRequest(
        [
            new BrokerReportOperationRequest(
                Guid.NewGuid(), BrokerReportOperationKind.BondTrade, default, null, null, null,
                null, null, null, null, null, null, null)
        ]));
        var nullOperationResult = await validator.ValidateAsync(new BrokerReportImportRequest([null!]));

        Assert.That(emptyResult.Errors.Single().ErrorMessage,
            Is.EqualTo("Выберите хотя бы одну операцию для добавления."));
        Assert.That(invalidOperationResult.Errors.Select(error => error.ErrorMessage),
            Does.Contain("Укажите дату каждой импортируемой операции."));
        Assert.That(nullOperationResult.Errors.Select(error => error.ErrorMessage),
            Does.Contain("В запросе обнаружены повторяющиеся или некорректные операции."));
    }

    [Test]
    public async Task Action_filter_returns_the_localized_fluent_validation_message()
    {
        var services = new ServiceCollection()
            .AddScoped<IValidator<BondSearchRequest>, BondSearchRequestValidator>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var controller = new object();
        var context = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?> { ["request"] = new BondSearchRequest { Query = "ab" } },
            controller);
        ActionExecutionDelegate next = () => Task.FromResult(new ActionExecutedContext(actionContext, [], controller));

        await new FluentValidationActionFilter().OnActionExecutionAsync(context, next);

        Assert.That(context.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)context.Result!).Value,
            Is.EqualTo("Введите не менее 3 символов для поиска."));
    }
}
