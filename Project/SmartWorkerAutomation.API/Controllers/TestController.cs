using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartWorkerAutomation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnly()
    {
        return Ok("You have successfully accessed this endpoint with the 'Admin' role!");
    }

    [HttpGet("super-admin-only")]
    [Authorize(Roles = "SuperAdmin")]
    public IActionResult SuperAdminOnly()
    {
        return Ok("You have successfully accessed this endpoint with the 'SuperAdmin' role!");
    }

    [HttpGet("authenticated-only")]
    [Authorize]
    public IActionResult AuthenticatedOnly()
    {
        return Ok("You have successfully accessed this endpoint as an authenticated user!");
    }
}
