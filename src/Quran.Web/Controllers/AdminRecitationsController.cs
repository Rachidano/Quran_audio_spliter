using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Quran.Web.Controllers;

// Retired historical circuit: all writes go through the canonical audio editor.
[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class RecitationsController : ControllerBase
{
    [HttpGet]
    [HttpPost("{id:long}/approve")]
    [HttpPost("{id:long}/publish")]
    [HttpPost("{id:long}/reject")]
    [HttpPost("{id:long}/wajh")]
    public IActionResult Retired() => StatusCode(StatusCodes.Status410Gone,
        new { message = "تم إلغاء مسار التحقق القديم. استخدم المراجعة والنشر.", reviewUrl = "/Admin/ReviewQueue" });
}
