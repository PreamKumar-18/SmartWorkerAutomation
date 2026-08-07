using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize]
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
    /// Reminder activity trend for the category. ?period=day (default, last
    /// 30 days) or ?period=month (last 12 months).
    /// </summary>
    [HttpGet("{category}/Trend")]
    public async Task<IActionResult> GetTrend(string category, [FromQuery] string period = "day")
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetReminderTrendAsync(category, period, userIdClaim, isSuperAdmin);
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
    /// Full detailed list of overdue/at-risk rows for the category - drives
    /// the detailed table on that category's dashboard page.
    /// </summary>
    [HttpGet("{category}/OverdueDetail")]
    public async Task<IActionResult> GetOverdueDetail(string category)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetOverdueDetailAsync(category, userIdClaim, isSuperAdmin);
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
    /// Active/inactive rule_alert_configuration counts by alert_type for the
    /// category - not user-scoped, since rule configuration is global data.
    /// </summary>
    [HttpGet("{category}/RuleHealth")]
    public async Task<IActionResult> GetRuleHealth(string category)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var data = await _dashboardService.GetRuleHealthAsync(category);
            return Ok(data);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
        }
    }

    /// <summary>
    /// Combined insights for the category dashboard: aging buckets, top-10
    /// at-risk entities, status breakdown, paused-reminders count, and the 5
    /// most recently resolved rows. See IDashboardService.GetInsightsAsync
    /// for what each field means per category.
    /// </summary>
    [HttpGet("{category}/Insights")]
    public async Task<IActionResult> GetInsights(string category)
    {
        try
        {
            var accessCheck = CheckCategoryAccess(category);
            if (accessCheck != null) return accessCheck;

            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetInsightsAsync(category, userIdClaim, isSuperAdmin);
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
    public async Task<IActionResult> GetLoginSummary()
    {
        try
        {
            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetLoginSummaryAsync(userIdClaim, isSuperAdmin);
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
    public async Task<IActionResult> GetTileDetail(string tileKey)
    {
        try
        {
            var (userIdClaim, isSuperAdmin) = ResolveIdentity();
            var data = await _dashboardService.GetTileDetailAsync(tileKey, userIdClaim, isSuperAdmin);
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
    /// scoping in ResolveIdentity(). Only 'User'-role accounts with a
    /// non-empty categories claim are restricted; Admin/SuperAdmin and
    /// Users with no explicit allowlist are always permitted (fail-open),
    /// so existing accounts created before this feature aren't locked out.
    /// Returns null when access is allowed, or a 403 result when denied.
    /// </summary>
    private IActionResult? CheckCategoryAccess(string category)
    {
        bool isUser = string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "User", StringComparison.OrdinalIgnoreCase);
        if (!isUser)
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
