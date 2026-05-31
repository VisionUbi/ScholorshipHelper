using Microsoft.AspNetCore.Mvc;

namespace ScholarshipAutoFill.Api.Controllers;

[ApiController]
[Route("api/research")]
public sealed class ResearchController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        status = "Not implemented",
        message = "Official source search and AI-assisted research start in Chunk 2."
    });
}
