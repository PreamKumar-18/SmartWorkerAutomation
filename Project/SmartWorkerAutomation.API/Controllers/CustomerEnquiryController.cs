using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

/// <summary>
/// Standalone Customer Enquiry CRUD screen (see Database/
/// create_customer_enquiries_table.sql). Backs the web-only, menu-hidden
/// /customer-enquiries route - insert/update/select + active/inactive
/// toggle only, deliberately no email/WhatsApp send endpoints here.
/// </summary>

[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[ApiController]
[Route("api/[controller]")]
public class CustomerEnquiryController : ControllerBase
{
    private readonly ICustomerEnquiryService _customerEnquiryService;

    public CustomerEnquiryController(ICustomerEnquiryService customerEnquiryService)
    {
        _customerEnquiryService = customerEnquiryService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var row = await _customerEnquiryService.GetByIdAsync(id);
        if (row is null)
        {
            return NotFound(new { message = "Customer enquiry not found." });
        }

        return Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerEnquiryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var createdBy = CurrentUserName();
            var (userId, _) = CurrentUserBranchContext();
            var created = await _customerEnquiryService.CreateAsync(request, createdBy, userId == 0 ? null : userId);
            return Ok(created);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the customer enquiry.", details = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCustomerEnquiryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updatedBy = CurrentUserName();
            var updated = await _customerEnquiryService.UpdateAsync(request, updatedBy);
            if (updated is null)
            {
                return NotFound(new { message = "Customer enquiry not found." });
            }

            return Ok(updated);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the customer enquiry.", details = ex.Message });
        }
    }

    /// <summary>Active/inactive toggle - the "update insert select active
    /// inactive" requirement's status half, kept as its own endpoint the
    /// same way RecordController.StatusUpdate is separate from a full edit.</summary>
    [HttpPost("SetActive")]
    public async Task<IActionResult> SetActive([FromBody] SetCustomerEnquiryActiveRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updatedBy = CurrentUserName();
            var updated = await _customerEnquiryService.SetActiveAsync(request.Id, request.IsActive, updatedBy);
            if (updated is null)
            {
                return NotFound(new { message = "Customer enquiry not found." });
            }

            return Ok(updated);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating status.", details = ex.Message });
        }
    }

    private string? CurrentUserName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>Same claim-resolution pattern InquiryController.GetInquiryData uses
    /// for its own branch entitlement check - userId comes off the JWT's sub/
    /// NameIdentifier claim, isSuperAdmin off the SuperAdmin role (either
    /// User.IsInRole or a plain role claim, whichever this token carries).
    /// Added alongside the branch_id/pipeline-fields migration so List can
    /// pass both through to CustomerEnquiry:List's new entitlement WHERE
    /// clause - this controller previously had no numeric-userId or
    /// superadmin resolution of any kind (only CurrentUserName() above, for
    /// CreatedBy/UpdatedBy attribution).</summary>
    private (int userId, bool isSuperAdmin) CurrentUserBranchContext()
    {
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = int.TryParse(userIdClaim, out var parsed) ? parsed : 0;

        var isSuperAdmin = User.IsInRole("SuperAdmin")
            || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        return (userId, isSuperAdmin);
    }
}
