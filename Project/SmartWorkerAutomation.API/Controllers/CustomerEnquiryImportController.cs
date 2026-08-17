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
[Authorize]
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

    [HttpPost("Upload")]
    [RequestSizeLimit(20_000_000)] // 20 MB
    public async Task<IActionResult> Upload(IFormFile file)
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

        try
        {
            await using var stream = file.OpenReadStream();
            var importedBy = CurrentUserName();
            var result = await _importService.ImportAsync(stream, file.FileName, importedBy);
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
}
