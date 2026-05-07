using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace tms_template_net8.Controllers.Api;

[ApiController]
[Route("index")]
public class IndexController : ControllerBase
{
    private readonly ILogger<IndexController> _logger;

    public IndexController(ILogger<IndexController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new { 
            message = "TMS API is running",
            version = "1.0.0", success = true,
            code = HttpStatusCode.OK
        });
    }
}
