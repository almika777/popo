using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Popo.Api;
using Popo.Core.Common;

namespace Popo.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Test]
    public async Task MapsApplicationFailuresToTheirHttpStatusAndMessage()
    {
        var cases = new (Exception Error, int StatusCode, string Message)[]
        {
            (new InvalidApplicationRequestException("invalid"), StatusCodes.Status400BadRequest, "invalid"),
            (new ApplicationResourceNotFoundException("missing"), StatusCodes.Status404NotFound, "missing"),
            (new ApplicationConflictException("conflict"), StatusCodes.Status409Conflict, "conflict"),
            (new InvalidOperationException("internal detail"), StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера. Попробуйте ещё раз.")
        };

        foreach (var testCase in cases)
        {
            var context = new DefaultHttpContext();
            await using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

            var handled = await handler.TryHandleAsync(context, testCase.Error, CancellationToken.None);

            responseBody.Position = 0;
            using var reader = new StreamReader(responseBody);
            Assert.Multiple(() =>
            {
                Assert.That(handled, Is.True);
                Assert.That(context.Response.StatusCode, Is.EqualTo(testCase.StatusCode));
                Assert.That(context.Response.ContentType, Is.EqualTo("text/plain; charset=utf-8"));
                Assert.That(reader.ReadToEnd(), Is.EqualTo(testCase.Message));
            });
        }
    }
}
