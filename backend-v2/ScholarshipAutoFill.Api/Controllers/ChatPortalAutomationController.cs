using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScholarshipAutoFill.Api.Data;
using ScholarshipAutoFill.Api.DTOs;
using ScholarshipAutoFill.Api.Services;

namespace ScholarshipAutoFill.Api.Controllers;

[ApiController]
[Route("api/chat-portal")]
public sealed class ChatPortalAutomationController(
    IChatPortalPlaywrightWorkflow workflow,
    IUniversityResearchPromptBuilder promptBuilder,
    AppDbContext db) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<ChatPortalLoginResponse>> Login([FromBody] ChatPortalLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetChatPortalUrl))
            return BadRequest(new { message = "Target chat portal URL is required." });

        try
        {
            await workflow.LoginAsync(request.TargetChatPortalUrl);
            return Ok(new ChatPortalLoginResponse(true, "Chat portal session saved locally for future requests."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("session-status")]
    public ActionResult<ChatPortalSessionStatusResponse> SessionStatus()
    {
        return Ok(new ChatPortalSessionStatusResponse(workflow.HasSavedSession()));
    }

    [HttpPost("build-prompt")]
    public async Task<ActionResult<ChatPortalPromptResponse>> BuildPrompt([FromBody] ChatPortalPromptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UniversityUrl))
            return BadRequest(new { message = "University URL is required." });

        var profile = await LoadProfileAsync(cancellationToken);
        return Ok(new ChatPortalPromptResponse(promptBuilder.BuildPrompt(request.UniversityUrl, profile)));
    }

    [HttpPost("query")]
    public async Task<ActionResult<ChatPortalQueryResponse>> Query([FromBody] ChatPortalQueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetChatPortalUrl))
            return BadRequest(new { message = "Target chat portal URL is required." });

        if (string.IsNullOrWhiteSpace(request.UniversityUrl))
            return BadRequest(new { message = "University URL is required." });

        try
        {
            var profile = await LoadProfileAsync(cancellationToken);
            var prompt = promptBuilder.BuildPrompt(request.UniversityUrl, profile);
            var result = await workflow.ExecuteQueryAsync(request.TargetChatPortalUrl, prompt);
            return Ok(new ChatPortalQueryResponse(prompt, result));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Task<Domain.ApplicantProfile> LoadProfileAsync(CancellationToken cancellationToken)
    {
        return db.ApplicantProfiles.OrderBy(x => x.CreatedAtUtc).FirstAsync(cancellationToken);
    }
}
