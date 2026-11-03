using FluentValidation;
using Popo.Api.Models;

namespace Popo.Api.Validators;

public sealed class ClosedPositionSessionsJobStatusRequestValidator
    : AbstractValidator<ClosedPositionSessionsJobStatusRequest>
{
    public ClosedPositionSessionsJobStatusRequestValidator()
    {
        RuleFor(request => request.JobId)
            .Must(jobId => !string.IsNullOrWhiteSpace(jobId)
                && jobId.Length <= 64
                && jobId.All(char.IsAsciiDigit))
            .WithMessage("Некорректный идентификатор задачи.");
    }
}
