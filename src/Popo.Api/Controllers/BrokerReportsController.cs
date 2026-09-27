using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services.BrokerReports;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/broker-reports")]
public sealed class BrokerReportsController(BrokerReportImportService importService) : ControllerBase
{
    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BrokerReportPreviewRequest.MaximumFileSizeBytes)]
    public async Task<ActionResult<BrokerReportPreviewResponse>> Preview(
        [FromForm] BrokerReportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = request.File!.OpenReadStream();
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
