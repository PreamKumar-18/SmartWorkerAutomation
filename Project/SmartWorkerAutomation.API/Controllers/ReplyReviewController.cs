using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

/// <summary>
/// Backs the Human Approval screen (see UI_integration_human_approval.md).
/// Open to every authenticated role (not Admin/SuperAdmin-only, unlike
/// UserController's management endpoints) - a plain 'User' account just has
/// the queue filtered down to their allowed categories, the same fail-open
/// rule as DashboardController/InquiryController's CheckCategoryAccess
/// (empty/null categories claim = no restriction).
/// </summary>
[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[ApiController]
[Route("api/[controller]")]
public class ReplyReviewController : ControllerBase
{
    private readonly IReplyReviewService _replyReviewService;

    public ReplyReviewController(IReplyReviewService replyReviewService)
    {
        _replyReviewService = replyReviewService;
    }

    [HttpGet]
    public async Task<IActionResult> GetQueue()
    {
        try
        {
            var rows = await _replyReviewService.GetHumanApprovalQueueAsync();
            return Ok(FilterByCategoryAccess(rows));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
        }
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveReplyRequest? request)
    {
        try
        {
            var reviewedBy = ResolveReviewedBy();
            var (found, applyResult) = await _replyReviewService.ApproveAsync(
                id,
                reviewedBy,
                request?.EditedPromisedDate,
                request?.EditedPromisedAmount);

            if (!found)
            {
                return NotFound(new { message = $"No reply intent found with id {id}." });
            }

            return Ok(new { message = "Reply approved and applied.", applyResult });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
        }
    }

    /// <summary>
    /// Live matched records for the category currently sitting in
    /// human_approval - backs the expand panel's "Current record" section
    /// with fresh data instead of the frozen business_data JSON snapshot
    /// stored on the reply itself. Only finance/purchase/inventory are
    /// offered (see ReplyReviewService.CategoryToViewMap - same hide as
    /// everywhere else Dispatch/Production is filtered out).
    /// </summary>
    [HttpGet("records-in-review")]
    public async Task<IActionResult> GetRecordsInReview([FromQuery] string category)
    {
        try
        {

            var rows = await _replyReviewService.GetRecordsInReviewAsync(category);
            return Ok(rows);
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
    /// Same apply as {id}/approve, keyed by the matched automation_records id
    /// instead of a reply_intent_id - backs the Human Approval table's Edit
    /// dialog Save, which only has the record id on hand (that table is built
    /// from records-in-review, not the reply_intents-driven queue). Calls
    /// fn_apply_reply_intent_by_record(), which finds whichever reply_intents
    /// row for this record is currently in human_approval status itself.
    /// </summary>
    [HttpPost("apply-by-record/{recordId}")]
    public async Task<IActionResult> ApplyByRecord(int recordId)
    {
        try
        {
            var (found, applyResult) = await _replyReviewService.ApplyByRecordAsync(recordId);

            if (!found)
            {
                return NotFound(new { message = $"No reply currently pending human approval for record {recordId}." });
            }

            return Ok(new { message = "Reply applied.", applyResult });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
        }
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        try
        {
            var reviewedBy = ResolveReviewedBy();
            var found = await _replyReviewService.RejectAsync(id, reviewedBy);

            if (!found)
            {
                return NotFound(new { message = $"No reply intent found with id {id}." });
            }

            return Ok(new { message = "Reply rejected." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing your request.", details = ex.Message });
        }
    }

    /// <summary>
    /// reply_intents.reviewed_by is a free-text audit column - the JWT's
    /// username claim (set at login, see TokenService.GenerateToken) reads
    /// better in a review trail than a raw numeric user id.
    /// </summary>
    private string ResolveReviewedBy()
    {
        return User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? "unknown";
    }

    /// <summary>
    /// Same rule as DashboardController/InquiryController's
    /// CheckCategoryAccess, for the one endpoint here that takes an explicit
    /// ?category= like those do, rather than spanning every category at once.
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
    /// Same category-access rule as DashboardController.CheckCategoryAccess,
    /// adapted for a queue that spans every category at once instead of a
    /// {category}-route-parameterized endpoint: a plain 'User' account with a
    /// non-empty categories claim only sees rows whose category_name is in
    /// their allowlist. Admin/SuperAdmin and Users with no explicit allowlist
    /// see everything (fail-open).
    /// </summary>
    private IEnumerable<dynamic> FilterByCategoryAccess(IEnumerable<dynamic> rows)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        bool isRestrictable = string.Equals(role, "User", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isRestrictable)
        {
            return rows;
        }

        var categoriesClaim = User.FindFirst("categories")?.Value;
        if (string.IsNullOrWhiteSpace(categoriesClaim))
        {
            return rows;
        }

        var allowed = categoriesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return rows.Cast<IDictionary<string, object>>()
            .Where(row =>
            {
                var categoryName = row.TryGetValue("category_name", out var value) ? value as string : null;
                return categoryName != null && allowed.Any(c => string.Equals(c, categoryName, StringComparison.OrdinalIgnoreCase));
            })
            .Cast<dynamic>();
    }
}
