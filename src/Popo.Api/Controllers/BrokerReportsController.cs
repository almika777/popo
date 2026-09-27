using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services.BrokerReports;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/broker-reports")]
public sealed class BrokerReportsController(BrokerReportImportService importService) : ControllerBase
{
    private const long MaximumPdfSizeBytes = 20 * 1024 * 1024;

    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPdfSizeBytes)]
    public async Task<ActionResult<BrokerReportPreviewResponse>> Preview(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Выберите PDF-файл брокерского отчёта.");

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || file.Length > MaximumPdfSizeBytes)
        {
            return BadRequest("Загрузите PDF-файл размером не более 20 МБ.");
        }

        await using var stream = file.OpenReadStream();
        var header = new byte[5];
        try
        {
            await stream.ReadExactlyAsync(header, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            return BadRequest("Загруженный файл не является корректным PDF-документом.");
        }

        if (!header.AsSpan().SequenceEqual("%PDF-"u8))
            return BadRequest("Загруженный файл не является корректным PDF-документом.");

        stream.Position = 0;
        try
        {
            return Ok(await importService.PreviewAsync(stream, cancellationToken));
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult<BrokerReportImportResponse>> Import(
        [FromBody] BrokerReportImportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await importService.ImportAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }
}
