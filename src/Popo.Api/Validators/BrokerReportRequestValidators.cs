using FluentValidation;
using Popo.Api.Models;
using Popo.Core.Common;
using Popo.Core.Portfolio;

namespace Popo.Api.Validators;

public sealed class BrokerReportPreviewRequestValidator : AbstractValidator<BrokerReportPreviewRequest>
{
    public BrokerReportPreviewRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("Выберите PDF-файл брокерского отчёта.")
            .Must(file => file!.Length > 0)
            .WithMessage("Выберите PDF-файл брокерского отчёта.")
            .Must(IsAllowedPdf)
            .WithMessage("Загрузите PDF-файл размером не более 20 МБ.")
            .MustAsync(HasPdfSignatureAsync)
            .WithMessage("Загруженный файл не является корректным PDF-документом.");
    }

    private static bool IsAllowedPdf(IFormFile? file) =>
        file is not null
        && Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
        && file.Length <= BrokerReportPreviewRequest.MaximumFileSizeBytes;

    private static async Task<bool> HasPdfSignatureAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return false;

        await using var stream = file.OpenReadStream();
        var header = new byte[5];
        try
        {
            await stream.ReadExactlyAsync(header, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            return false;
        }

        return header.AsSpan().SequenceEqual("%PDF-"u8);
    }
}

public sealed class BrokerReportImportRequestValidator : AbstractValidator<BrokerReportImportRequest>
{
    public BrokerReportImportRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Operations)
            .NotEmpty()
            .WithMessage("Выберите хотя бы одну операцию для добавления.")
            .Must(HasUniqueValidIds)
            .WithMessage("В запросе обнаружены повторяющиеся или некорректные операции.");

        RuleForEach(request => request.Operations)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("В запросе обнаружены повторяющиеся или некорректные операции.")
            .SetValidator(new BrokerReportOperationRequestValidator())
            .When(request => request.Operations is not null);
    }

    private static bool HasUniqueValidIds(IReadOnlyList<BrokerReportOperationRequest>? operations) =>
        operations is not null
        && operations.All(operation => operation is not null && operation.Id != Guid.Empty)
        && operations.Select(operation => operation.Id).Distinct().Count() == operations.Count;
}

public sealed class BrokerReportOperationRequestValidator : AbstractValidator<BrokerReportOperationRequest>
{
    public BrokerReportOperationRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(operation => operation.Date)
            .Must(date => date != default)
            .WithMessage("Укажите дату каждой импортируемой операции.");
        RuleFor(operation => operation.Commission)
            .Must(commission => !commission.HasValue || double.IsFinite(commission.Value) && commission.Value >= 0)
            .WithMessage("Комиссия должна быть неотрицательной.");
        RuleFor(operation => operation.Kind)
            .IsInEnum()
            .WithMessage("Неизвестный тип операции в запросе импорта.");

        When(operation => operation.Kind == BrokerReportOperationKind.BondTrade, () =>
        {
            RuleFor(operation => operation.SecId)
                .Must(HasText)
                .WithMessage("Укажите код облигации.");
            RuleFor(operation => operation.BoardId)
                .Must(HasText)
                .WithMessage("Укажите режим торгов облигации.");
            RuleFor(operation => operation.CurrencyId)
                .Must(HasText)
                .WithMessage("Укажите валюту сделки.");
            RuleFor(operation => operation.Side)
                .Must(IsValidSide)
                .WithMessage("Выберите покупку или продажу.");
            RuleFor(operation => operation.Quantity)
                .Must(IsPositiveFinite)
                .WithMessage("Количество должно быть положительным.");
            RuleFor(operation => operation.UnitPrice)
                .Must(IsPositiveFinite)
                .WithMessage("Цена должна быть положительной.");
            RuleFor(operation => operation.FaceValue)
                .Must(IsPositiveFinite)
                .WithMessage("Номинал облигации должен быть положительным.");
            RuleFor(operation => operation.AccruedInterestTotal)
                .Must(IsNonNegativeFinite)
                .WithMessage("НКД должен быть неотрицательным.");
        });

        When(operation => operation.Kind == BrokerReportOperationKind.FundOperation, () =>
        {
            RuleFor(operation => operation.SecId)
                .Must(HasText)
                .WithMessage("Укажите код фонда.");
            RuleFor(operation => operation.SecId)
                .Must(secId => !HasText(secId) || RelationHelper.MoneyMarketFunds.ContainsKey(secId!.Trim()))
                .WithMessage("Поддерживаются только LQDT и TMON.");
            RuleFor(operation => operation.Side)
                .Must(IsValidSide)
                .WithMessage("Выберите покупку или продажу.");
            RuleFor(operation => operation.Quantity)
                .Must(IsPositiveFinite)
                .WithMessage("Количество паёв должно быть положительным.");
            RuleFor(operation => operation.Quantity)
                .Must(quantity => quantity.HasValue && quantity.Value == Math.Truncate(quantity.Value))
                .WithMessage("Количество паёв фонда должно быть целым числом.")
                .When(operation => IsPositiveFinite(operation.Quantity));
            RuleFor(operation => operation.UnitPrice)
                .Must(IsPositiveFinite)
                .WithMessage("Цена пая должна быть положительной.");
        });

        When(operation => operation.Kind is BrokerReportOperationKind.Deposit or BrokerReportOperationKind.Withdrawal, () =>
            RuleFor(operation => operation.Amount)
                .Must(IsPositiveFinite)
                .WithMessage("Сумма пополнения или вывода должна быть положительной."));
    }

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsValidSide(TradeSide? side) => side.HasValue && Enum.IsDefined(side.Value);

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value > 0;

    private static bool IsNonNegativeFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value) && value.Value >= 0;
}
