using System;
using System.Security.Claims;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RecordController : ControllerBase
{
    private readonly IInquiryService _inquiryService;

    public RecordController(IInquiryService inquiryService)
    {
        _inquiryService = inquiryService;
    }

    /// <summary>
    /// Updates the file status of an automation record to true/false based on 'active'/'inactive' input.
    /// </summary>
    [HttpPost("FileStatusUpdate")]
    public async Task<IActionResult> UpdateFileStatus([FromBody] UpdateFileStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            bool isSuperAdmin = User.IsInRole("SuperAdmin") 
                                || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            var success = await _inquiryService.UpdateFileStatusAsync(request.Id, request.Status, userIdClaim ?? string.Empty, isSuperAdmin);
            if (!success)
            {
                return NotFound(new { message = "Record not found or user does not have permission to update it." });
            }

            return Ok(new { message = "File status updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
        }
    }
}
