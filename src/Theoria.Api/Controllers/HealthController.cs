using Microsoft.AspNetCore.Mvc;

namespace Theoria.Api.Controllers;

/// <summary>
/// Health check endpoint for container orchestration and monitoring.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// GET /health
    /// Returns 200 OK if the service is running.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
