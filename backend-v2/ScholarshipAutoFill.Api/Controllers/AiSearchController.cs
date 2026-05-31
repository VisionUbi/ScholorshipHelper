using Microsoft.AspNetCore.Mvc;
using ScholarshipAutoFill.Api.DTOs;
using ScholarshipAutoFill.Api.Services;

namespace ScholarshipAutoFill.Api.Controllers;

[ApiController]
[Route("api/ai-search")]
public sealed class AiSearchController(IAiSearchService aiSearchService) : ControllerBase
{
    [HttpGet("provider-status")]
    public ActionResult<AiSearchProviderStatusResponse> ProviderStatus()
    {
        return Ok(aiSearchService.GetProviderStatus());
    }

    [HttpPost("generate-prompt")]
    public async Task<ActionResult<AiSearchPromptResponse>> GeneratePrompt([FromBody] AiSearchCriteriaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { message = "University URL, scholarship URL, program URL, or university name is required." });

        return Ok(await aiSearchService.GeneratePromptAsync(request, cancellationToken));
    }

    [HttpPost("parse-response")]
    public ActionResult<AiSearchParseResponse> ParseResponse([FromBody] AiSearchParseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JsonResponse))
            return BadRequest(new { message = "Paste the JSON response first." });

        return Ok(aiSearchService.ParseResponse(request));
    }

    [HttpPost("save-results")]
    public async Task<ActionResult<AiSearchSaveResponse>> SaveResults([FromBody] AiSearchSaveRequest request, CancellationToken cancellationToken)
    {
        if (request.Universities.Length == 0)
            return BadRequest(new { message = "No parsed university results were provided." });

        return Ok(await aiSearchService.SaveResultsAsync(request, cancellationToken));
    }

    [HttpPost("search")]
    public async Task<ActionResult<AiSearchParseResponse>> Search([FromBody] AiSearchCriteriaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { message = "University URL, scholarship URL, program URL, or university name is required." });

        return Ok(await aiSearchService.SearchAsync(request, cancellationToken));
    }
}
