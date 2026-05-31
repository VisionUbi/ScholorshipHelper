using Microsoft.AspNetCore.Mvc;
using ScholarshipAutoFill.Api.DTOs;
using ScholarshipAutoFill.Api.Services;

namespace ScholarshipAutoFill.Api.Controllers;

[ApiController]
[Route("api/profile")]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApplicantProfileDto>> Get(CancellationToken cancellationToken)
    {
        var profile = await profileService.GetAsync(cancellationToken);
        return profile is null ? NotFound(new { message = "Applicant profile has not been created yet." }) : Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<ApplicantProfileDto>> Upsert([FromBody] UpsertApplicantProfileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var profile = await profileService.UpsertAsync(request, cancellationToken);
        return Ok(profile);
    }
}
