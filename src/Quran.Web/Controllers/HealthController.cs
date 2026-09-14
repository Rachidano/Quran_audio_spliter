using Microsoft.AspNetCore.Mvc;

namespace Quran.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Platform = "Quran Warsh - Cheikh Saber Abdelhakam",
            Version = "1.0 (.NET 9)",
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
