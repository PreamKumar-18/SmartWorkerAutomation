using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[ApiController]
[Route("api/[controller]")]
public class InquiryController : ControllerBase
{
    private readonly IInquiryService _inquiryService;
    private readonly IRecordsExportService _exportService;

    public InquiryController(IInquiryService inquiryService, IRecordsExportService exportService)
    {
        _inquiryService = inquiryService;
        _exportService = exportService;
    }

    /// <summary>
    /// branchId (0 = All Branches the caller belongs to), sortColumn/sortDir,
    /// filters (a JSON object string, e.g. {"material_status":"Pending"}),
    /// and page/pageSize are all optional - omitting them behaves exactly as
    /// before (everything the caller can see, sorted by id desc). Branch
    /// entitlement is no longer read from a JWT claim here - it's checked
    /// live inside fn_get_{category}_records against user_branch, so a
    /// stale/forged branchId just returns nothing rather than a wrong
    /// dataset. See IInquiryService.GetInquiryDataAsync for the full
    /// contract.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInquiryData(
        [FromQuery] string category,
        [FromQuery] int branchId = 0,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDir = null,
        [FromQuery] string? filters = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            bool isSuperAdmin = User.IsInRole("SuperAdmin")
                    || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                    || User.IsInRole("Admin")
                    || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "Admin", StringComparison.OrdinalIgnoreCase);

            var categoriesClaim = User.FindFirst("categories")?.Value;
            var allowedCategories = string.IsNullOrEmpty(categoriesClaim)
                ? null
                : categoriesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries);    

            var data = await _inquiryService.GetInquiryDataAsync(
                category, userIdClaim ?? string.Empty, isSuperAdmin,
                branchId, sortColumn, sortDir, filters, page, pageSize);
            return Ok(data);
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
    /// Total row count for the same category/branchId/filters combination
    /// GetInquiryData would use - lets the frontend build "page 1 of N"
    /// without fetching every row just to count them. Only supports the 5
    /// branch-scoped business categories (see
    /// IInquiryService.GetInquiryCountAsync) - anything else 400s.
    /// </summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetInquiryCount(
        [FromQuery] string category,
        [FromQuery] int branchId = 0,
        [FromQuery] string? filters = null)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            bool isSuperAdmin = User.IsInRole("SuperAdmin")
                                || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            var count = await _inquiryService.GetInquiryCountAsync(
                category, userIdClaim ?? string.Empty, isSuperAdmin, branchId, filters);
            return Ok(new { totalCount = count });
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
    /// Single-record read straight from the category view (finance_view/
    /// purchase_view/inventory_view/etc.) - backs the Records page's
    /// row-click detail drawer, which wants one fresh, authoritative row
    /// instead of trusting whatever copy is still sitting in the
    /// already-loaded table array (can go stale after another change
    /// elsewhere touches the same record). Same category access check and
    /// superadmin/owned-row scoping as GET above.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetInquiryRecordById(int id, [FromQuery] string category)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            bool isSuperAdmin = User.IsInRole("SuperAdmin")
                                || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            var data = await _inquiryService.GetRecordByIdAsync(category, id, userIdClaim ?? string.Empty, isSuperAdmin);

            if (data is null)
            {
                return NotFound(new { message = $"No {category} record found with id {id}." });
            }

            return Ok(data);
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
    /// Downloads every category's records as one .xlsx workbook, one
    /// worksheet per category - backs the Records page's Download button.
    /// branchId (0 = All Branches the caller belongs to, default) scopes
    /// every sheet to whichever branch is currently selected on screen,
    /// matching exactly what the table shows - same convention as GET
    /// above. A role-restricted User only gets sheets for the categories in
    /// their own "categories" claim (same restriction CheckCategoryAccess
    /// enforces per-request elsewhere in this controller, applied here as a
    /// sheet filter instead of an all-or-nothing 403 - unlike a
    /// single-category endpoint, there's no single "category" to reject the
    /// whole request over). Same isSuperAdmin/userId row-level scoping as
    /// GET above either way.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportRecords([FromQuery] int branchId = 0)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            bool isSuperAdmin = User.IsInRole("SuperAdmin")
                                || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            var allowedCategories = GetAllowedCategories();

            var fileBytes = await _exportService.ExportAllToExcelAsync(userIdClaim ?? string.Empty, isSuperAdmin, allowedCategories, branchId);
            var fileName = $"records-export-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
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
    /// Field-level edit for a single record - backs every "Edit" dialog that
    /// reuses RecordsService.update*() on the frontend (Records/Data
    /// Management page, all 5 Category Dashboard Overdue Detail tables, and
    /// both Pending Actions tables). Only category-registered editable
    /// fields (InquiryService.EditableFields) are ever written; anything
    /// else in the body is ignored rather than erroring, so extra
    /// read-only fields the dialog also submits don't break the call.
    /// </summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateRecord(int id, [FromQuery] string category, [FromBody] Dictionary<string, JsonElement>? changes)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            bool isSuperAdmin = User.IsInRole("SuperAdmin")
                                || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            var updated = await _inquiryService.UpdateRecordAsync(
                category,
                id,
                changes ?? new Dictionary<string, JsonElement>(),
                userIdClaim ?? string.Empty,
                isSuperAdmin);

            if (updated is null)
            {
                return NotFound(new { message = $"No {category} record found with id {id}." });
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
    /// Category-level access check - see DashboardController.CheckCategoryAccess
    /// for the full rationale (fail-open when the categories claim is empty).
    /// Restricts 'User' AND 'Admin' role accounts now (was 'User'-only) -
    /// Admin became a selectable role for AllowedCategories in the user
    /// creation/edit forms, so an Admin with an explicit allowlist should be
    /// held to it the same way a restricted User already is; SuperAdmin
    /// stays unconditionally unrestricted.
    ///
    /// Sale (customerenquiry) used to be unconditionally exempted from this
    /// check entirely - CATEGORY_OPTIONS (the frontend's allowed-categories
    /// checkbox list) never included it, so no account's "categories" claim
    /// could ever contain "customerenquiry", and gating on it would have
    /// 403'd every restricted account rather than just ones deliberately
    /// denied Sale. Now that "Sales" is a real checkbox option, that
    /// exemption is gone - Sale is gated exactly like Finance/Purchase/
    /// Inventory: unchecked for a restricted account = no access, same as
    /// any other category. Existing restricted accounts that relied on the
    /// old blanket exemption need "Sales" explicitly added to their allowed
    /// categories now if they still need it.
    /// </summary>
    private IActionResult? CheckCategoryAccess(string category)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        bool isRestrictable = string.Equals(role, "User", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isRestrictable)
        {
            return null;
        }

        var categoriesClaim = User.FindFirst("categories")?.Value;
        if (string.IsNullOrWhiteSpace(categoriesClaim))
        {
            return null;
        }

        var allowed = categoriesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowed.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return StatusCode(403, new { message = $"You do not have access to the '{category}' category." });
    }

    /// <summary>
    /// Same claim/role logic as CheckCategoryAccess, but returns the allowed
    /// set instead of gating a single category - null means unrestricted
    /// (not a restrictable role, or the claim is empty, matching
    /// CheckCategoryAccess's fail-open behavior in both those cases).
    /// </summary>
    private List<string>? GetAllowedCategories()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        bool isRestrictable = string.Equals(role, "User", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isRestrictable)
        {
            return null;
        }

        var categoriesClaim = User.FindFirst("categories")?.Value;
        if (string.IsNullOrWhiteSpace(categoriesClaim))
        {
            return null;
        }

        return categoriesClaim
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
