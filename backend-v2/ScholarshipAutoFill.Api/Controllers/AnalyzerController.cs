using Microsoft.AspNetCore.Mvc;
using ScholarshipAutoFill.Api.DTOs;
using ScholarshipAutoFill.Api.Services;

namespace ScholarshipAutoFill.Api.Controllers;

[ApiController]
[Route("api/analyzer")]
public sealed class AnalyzerController(IAiResearchService researchService) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        status = "Chunk 1 ready",
        message = "AI research pipeline will be added in Chunk 2."
    });

    [HttpPost("analyze")]
    public async Task<ActionResult<ScholarshipAnalyzeResponse>> Analyze([FromBody] ScholarshipAnalyzeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { message = "University URL, scholarship URL, program URL, or university name is required." });

        var result = await researchService.AnalyzeAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/scholarship-ai/analyze")]
    public Task<ActionResult<ScholarshipAnalyzeResponse>> AnalyzeLegacyRoute([FromBody] ScholarshipAnalyzeRequest request, CancellationToken cancellationToken)
    {
        return Analyze(request, cancellationToken);
    }
}
