using System;
using System.Security.Claims;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
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

    /// <summary>
    /// Quick status action for a Records table/list row - Finance "Mark as
    /// paid" and Purchase "Mark delivered" / "Mark received". Only changes
    /// the one status field (see InquiryService.UpdateRecordStatusAsync);
    /// use the existing PATCH /api/Inquiry/{id} edit endpoint for full-record
    /// edits instead. Same REST endpoint backs both the web app and the
    /// mobile app - no platform-specific backend work needed, only the
    /// client-side row/card action button.
    /// </summary>
    [HttpPost("StatusUpdate")]
    public async Task<IActionResult> UpdateRecordStatus([FromBody] UpdateRecordStatusRequest request)
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

            var updated = await _inquiryService.UpdateRecordStatusAsync(request.Category, request.Id, request.Status, userIdClaim ?? string.Empty, isSuperAdmin);
            if (updated is null)
            {
                return NotFound(new { message = "Record not found or user does not have permission to update it." });
            }

            return Ok(updated);
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

    /// <summary>
    /// Records drawer's Promise to pay section - "Promised amount"/"Promised
    /// by" (automation_records.promised_amount/snooze_until). No category in
    /// the request - see InquiryService.UpdatePromiseToPayAsync's own doc
    /// comment for why this endpoint is id-only, unlike StatusUpdate above.
    /// </summary>
    [HttpPost("PromiseToPay")]
    public async Task<IActionResult> UpdatePromiseToPay([FromBody] UpdatePromiseToPayRequest request)
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

            var success = await _inquiryService.UpdatePromiseToPayAsync(request.Id, request.PromisedAmount, request.PromisedBy, userIdClaim ?? string.Empty, isSuperAdmin);
            if (!success)
            {
                return NotFound(new { message = "Record not found or user does not have permission to update it." });
            }

            return Ok(new { message = "Promise to pay updated successfully." });
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

    /// <summary>
    /// Records drawer's Call action (Finance only, initial rollout) - see
    /// InquiryService.InitiateCallAsync's own doc comment for the full round
    /// trip. Returns 200 with { phoneNumber, autoDialTriggered } either way
    /// (never fails just because auto-dial didn't fire) - only 404s when the
    /// record itself doesn't exist/isn't this user's.
    /// </summary>
    [HttpPost("Call")]
    public async Task<IActionResult> InitiateCall([FromBody] InitiateCallRequest request)
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

            var result = await _inquiryService.InitiateCallAsync(request.Category, request.Id, userIdClaim ?? string.Empty, isSuperAdmin);
            if (result is null)
            {
                return NotFound(new { message = "Record not found or user does not have permission to call it." });
            }

            return Ok(result);
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
