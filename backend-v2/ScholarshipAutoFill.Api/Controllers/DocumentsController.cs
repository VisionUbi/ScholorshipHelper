using Microsoft.AspNetCore.Mvc;

namespace ScholarshipAutoFill.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        status = "Not implemented",
        message = "Document storage will be added after the research/profile foundation is stable."
    });
}
