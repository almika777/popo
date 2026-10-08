using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Recommendations;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class CashRecommendationsController(
    ICashRecommendationService cashRecommendationService,
    CashRecommendationsPageService pageService,
    CashRecommendationSettingsService settingsService) : ControllerBase
{
    [HttpGet("cash/page")]
    public async Task<ActionResult<CashRecommendationsPageResponse>> GetPage(
        CancellationToken cancellationToken) =>
        Ok(await pageService.GetAsync(cancellationToken));

    [HttpGet("cash")]
    public async Task<ActionResult<CashRecommendationResponse>> GetCurrent(
        CancellationToken cancellationToken)
    {
        var result = await cashRecommendationService.GetRecommendations(cancellationToken);
        return Ok(CashRecommendationApiMapper.ToResponse(result));
    }

    [HttpGet("cash/settings")]
    public async Task<ActionResult<InvestmentStrategySettingsResponse>> GetSettings(
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(settings));
    }

    [HttpGet("cash/presets")]
    public async Task<ActionResult<IReadOnlyList<InvestmentStrategyPresetResponse>>> GetPresets(
        CancellationToken cancellationToken)
    {
        var presets = await settingsService.GetPresetsAsync(cancellationToken);
        return Ok(presets.Select(CashInvestmentRecommendationApiMapper.ToResponse));
    }

    [HttpPost("cash/presets")]
    public async Task<ActionResult<InvestmentStrategyPresetResponse>> CreatePreset(
        [FromBody] InvestmentStrategyPresetRequest request, CancellationToken cancellationToken)
    {
        var preset = await settingsService.CreatePresetAsync(request, cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(preset));
    }

    [HttpPut("cash/presets/{id:int}")]
    public async Task<ActionResult<InvestmentStrategyPresetResponse>> UpdatePreset(int id,
        [FromBody] InvestmentStrategyPresetRequest request, CancellationToken cancellationToken)
    {
        var preset = await settingsService.UpdatePresetAsync(id, request, cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(preset));
    }

    [HttpDelete("cash/presets/{id:int}")]
    public async Task<IActionResult> DeletePreset(int id, CancellationToken cancellationToken)
    {
        await settingsService.DeletePresetAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("cash/presets/{id:int}/activate")]
    public async Task<IActionResult> ActivatePreset(int id, CancellationToken cancellationToken)
    {
        await settingsService.ActivatePresetAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("cash/settings")]
    public async Task<ActionResult<InvestmentStrategySettingsResponse>> UpdateSettings(
        [FromBody] InvestmentStrategySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.UpdateSettingsAsync(request.ToDomain(), cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(settings));
    }

    [HttpPost("cash/recalculate")]
    public async Task<ActionResult<CashRecommendationResponse>> Recalculate(
        CancellationToken cancellationToken)
    {
        var result = await cashRecommendationService.GetRecommendations(cancellationToken);
        return Ok(CashRecommendationApiMapper.ToResponse(result));
    }
}
