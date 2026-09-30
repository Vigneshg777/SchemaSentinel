using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SchemaSentinel.Application.Analysis;

namespace SchemaSentinel.Api.Controllers;

/// <summary>Request payload for analyzing a migration script.</summary>
public sealed class AnalyzeRequestDto
{
    [Required(ErrorMessage = "A migration script is required.")]
    public string Script { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ScriptName { get; set; }
}

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController(
    IAnalysisService analysisService,
    IAnalysisHistoryService historyService,
    ILogger<AnalysisController> logger) : ControllerBase
{
    /// <summary>Analyzes a migration script and stores the result in history.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analyze(
        [FromBody] AnalyzeRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await analysisService.AnalyzeAsync(
                new AnalyzeMigrationRequest(request.Script, request.ScriptName),
                cancellationToken);

            try
            {
                await historyService.SaveAsync(result, request.Script, cancellationToken);
            }
            catch (Exception ex)
            {
                // History persistence must not break the analysis response.
                logger.LogError(ex, "Failed to persist analysis history for {AnalysisId}.", result.AnalysisId);
            }

            return Ok(result);
        }
        catch (MigrationValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid migration script");
        }
    }

    /// <summary>Returns the most recent analyses.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        var items = await historyService.GetRecentAsync(take, cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns a previously saved analysis by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await historyService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Deletes a saved analysis.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await historyService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
