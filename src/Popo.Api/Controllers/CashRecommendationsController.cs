using Microsoft.AspNetCore.Mvc;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Recommendations;

namespace Popo.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class CashRecommendationsController(
    ICashInvestmentRecommendationStore store,
    ICashRecommendationService cashRecommendationService,
    CashRecommendationsPageService pageService) : ControllerBase
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
        var settings = await store.GetSettingsAsync(cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(settings));
    }

    [HttpGet("cash/presets")]
    public async Task<ActionResult<IReadOnlyList<InvestmentStrategyPresetResponse>>> GetPresets(
        CancellationToken cancellationToken) =>
        Ok((await store.GetPresetsAsync(cancellationToken)).Select(CashInvestmentRecommendationApiMapper.ToResponse));

    [HttpPost("cash/presets")]
    public async Task<ActionResult<InvestmentStrategyPresetResponse>> CreatePreset(
        [FromBody] InvestmentStrategyPresetRequest request, CancellationToken cancellationToken)
    {
        var preset = await store.SavePresetAsync(
            new InvestmentStrategyPreset(0, request.Name, request.Settings.ToDomain(), false), cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(preset));
    }

    [HttpPut("cash/presets/{id:int}")]
    public async Task<ActionResult<InvestmentStrategyPresetResponse>> UpdatePreset(int id,
        [FromBody] InvestmentStrategyPresetRequest request, CancellationToken cancellationToken)
    {
        var current = await store.GetPresetAsync(id, cancellationToken);
        if (current is null) return NotFound("Пресет не найден.");
        var preset =
            await store.SavePresetAsync(current with { Name = request.Name, Settings = request.Settings.ToDomain() },
                cancellationToken);
        return Ok(CashInvestmentRecommendationApiMapper.ToResponse(preset));
    }

    [HttpDelete("cash/presets/{id:int}")]
    public async Task<IActionResult> DeletePreset(int id, CancellationToken cancellationToken) =>
        await store.DeletePresetAsync(id, cancellationToken) ? NoContent() : NotFound("Пресет не найден.");

    [HttpPost("cash/presets/{id:int}/activate")]
    public async Task<IActionResult> ActivatePreset(int id, CancellationToken cancellationToken) =>
        await store.ActivatePresetAsync(id, cancellationToken) ? NoContent() : NotFound("Пресет не найден.");

    [HttpPut("cash/settings")]
    public async Task<ActionResult<InvestmentStrategySettingsResponse>> UpdateSettings(
        [FromBody] InvestmentStrategySettingsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("Параметры стратегии не переданы.");

        var settings = request.ToDomain();
        if (!await store.SaveSettingsAsync(settings, cancellationToken))
            return Conflict("Не удалось сохранить настройки стратегии.");

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
