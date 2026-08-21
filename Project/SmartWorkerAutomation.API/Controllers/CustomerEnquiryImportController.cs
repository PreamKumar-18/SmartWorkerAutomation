using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

/// <summary>
/// Bulk-load path for the standalone Customer Enquiry screen - a separate
/// controller/route from CustomerEnquiryController (api/CustomerEnquiry) so
/// the existing single-row CRUD endpoints there are never touched by this.
/// GetTemplate hands back a blank .xlsx with the expected header row;
/// Upload accepts that same template (or a .csv with matching headers)
/// filled in, and inserts every row whose (contact_name, customer_name)
/// pair isn't already in customer_enquiries - see
/// CustomerEnquiryImportService for the exact duplicate-skip rule.
/// </summary>
[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[ApiController]
[Route("api/[controller]")]
public class CustomerEnquiryImportController : ControllerBase
{
    private readonly ICustomerEnquiryImportService _importService;

    public CustomerEnquiryImportController(ICustomerEnquiryImportService importService)
    {
        _importService = importService;
    }

    [HttpGet("Template")]
    public IActionResult GetTemplate()
    {
        var bytes = _importService.BuildTemplateWorkbook();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "customer-enquiry-template.xlsx");
    }

    /// <summary>
    /// branchId is required, same guard IngestionController.UploadFile
    /// enforces for the Records upload path - customer_enquiries.branch_id
    /// has no sane default to fall back to (a bulk-uploaded batch has no
    /// per-row branch column of its own to source it from), so the caller
    /// must have a specific branch selected (never the "All Branches"
    /// pseudo-selection) before uploading. See
    /// CustomerEnquiryImportBarComponent.onFileSelected for the matching
    /// frontend guard.
    /// </summary>
    [HttpPost("Upload")]
    [RequestSizeLimit(20_000_000)] // 20 MB
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] int? branchId)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls" && extension != ".csv")
        {
            return BadRequest(new { message = "Only .xlsx, .xls, or .csv files are supported." });
        }

        if (branchId is null || branchId == 0)
        {
            return BadRequest(new { message = "Select a specific branch before uploading." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var importedBy = CurrentUserName();
            var userId = CurrentUserId();
            var result = await _importService.ImportAsync(stream, file.FileName, importedBy, userId, branchId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while importing the file.", details = ex.Message });
        }
    }

    private string? CurrentUserName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>Numeric "User"."UserId" off the JWT sub/NameIdentifier claim -
    /// same resolution CustomerEnquiryController.CurrentUserBranchContext
    /// uses, duplicated here rather than shared since this controller has no
    /// branch-entitlement (isSuperAdmin) half to go with it. Null if the
    /// claim is missing/non-numeric, so a malformed token still fails soft
    /// (user_id just stays null on the imported rows) instead of 500ing the
    /// whole upload.</summary>
    private int? CurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var parsed) ? parsed : null;
    }
}
