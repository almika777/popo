using Microsoft.AspNetCore.Diagnostics;
using Popo.Core.Common;

namespace Popo.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message) = exception switch
        {
            InvalidApplicationRequestException => (StatusCodes.Status400BadRequest, exception.Message),
            ApplicationResourceNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ApplicationConflictException => (StatusCodes.Status409Conflict, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера. Попробуйте ещё раз.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Необработанная ошибка API.");
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "text/plain; charset=utf-8";
        await httpContext.Response.WriteAsync(message, cancellationToken);
        return true;
    }
}
