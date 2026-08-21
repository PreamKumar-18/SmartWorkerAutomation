using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// KPI summary for one category's dashboard page - total records,
    /// overdue/at-risk count, overdue amount where applicable, reminders
    /// sent today.
    /// </summary>
    [HttpGet("{category}/Summary")]
    public async Task<IActionResult> GetSummary(string category)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetCategorySummaryAsync(category, userIdClaim, isSuperAdmin);
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
    /// Real send/reply history for one record - powers the Journey expand
    /// panel on the Overdue Detail table (notification_log + inbound_messages,
    /// ordered chronologically). ?id is the category view's row id (same id
    /// notification_log.record_id / inbound_messages.matched_record_id
    /// reference).
    /// </summary>
    [HttpGet("{category}/Journey/{id}")]
    public async Task<IActionResult> GetJourney(string category, int id)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetRecordJourneyAsync(category, id, userIdClaim, isSuperAdmin);
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
    /// Cross-category KPIs for the post-login landing page - not scoped to
    /// any one category, so no CheckCategoryAccess call here (every
    /// authenticated role sees this, same as the page it backs).
    /// </summary>
    [HttpGet("LoginSummary")]
    public async Task<IActionResult> GetLoginSummary([FromQuery] int branchId = 0)
    {
        try
        {
            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetLoginSummaryAsync(userIdClaim, isSuperAdmin, branchId);
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
    /// The record rows behind one Overview/LoginSummary tile - powers the
    /// tile-detail panel on the Overview page when a tile is clicked. Not
    /// scoped to any one category (same reasoning as LoginSummary above -
    /// tileKey itself picks Finance vs Purchase internally).
    /// </summary>
    [HttpGet("TileDetail/{tileKey}")]
    public async Task<IActionResult> GetTileDetail(string tileKey, [FromQuery] int branchId = 0)
    {
        try
        {
            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetTileDetailAsync(tileKey, userIdClaim, isSuperAdmin, branchId);
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

    private (string userIdClaim, bool isSuperAdmin) ResolveIdentity()
    {
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        bool isSuperAdmin = User.IsInRole("SuperAdmin")
                            || string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        return (userIdClaim ?? string.Empty, isSuperAdmin);
    }

    /// <summary>
    /// Category-level access check, distinct from the userid ownership
    /// scoping in ResolveIdentity(). 'User' and 'Admin' role accounts with a
    /// non-empty categories claim are restricted (Admin added alongside
    /// AllowedCategories becoming selectable for Admin in the user
    /// creation/edit forms - was 'User'-only before); SuperAdmin and any
    /// restrictable account with no explicit allowlist are always permitted
    /// (fail-open), so existing accounts created before this feature aren't
    /// locked out. Returns null when access is allowed, or a 403 result when
    /// denied.
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
}
