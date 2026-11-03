using FluentValidation;
using Popo.Api.Models;
using Popo.Api.Services;

namespace Popo.Api.Validators;

public sealed class IssuerLogoUploadRequestValidator : AbstractValidator<IssuerLogoUploadRequest>
{
    public const long MaximumFileSize = 2 * 1024 * 1024;

    public IssuerLogoUploadRequestValidator()
    {
        RuleFor(request => request.EmitterId)
            .GreaterThan(0)
            .WithMessage("Выберите эмитента.");

        RuleFor(request => request.File)
            .NotNull()
            .WithMessage("Выберите файл логотипа.")
            .Must(file => file is { Length: > 0 and <= MaximumFileSize })
            .WithMessage("Размер логотипа должен быть от 1 байта до 2 МБ.")
            .Must(file => file is not null && IsSupportedExtension(file.FileName))
            .WithMessage("Поддерживаются только файлы PNG и SVG.")
            .MustAsync(HasValidImageContentAsync)
            .WithMessage("Содержимое файла не соответствует формату PNG или SVG.");
    }

    private static async Task<bool> HasValidImageContentAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > MaximumFileSize)
            return true;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".png" or ".svg"))
            return true;

        var content = new byte[checked((int)file.Length)];
        await using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(content, cancellationToken);
        return IssuerLogoFileStore.IsValidImage(content, extension);
    }

    private static bool IsSupportedExtension(string fileName) =>
        Path.GetExtension(fileName) is { } extension
        && (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase));
}
