using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SmartWorkerAutomation.DataProvider.Automation;

namespace SmartWorkerAutomation.API.Controllers;

[Authorize(AuthenticationSchemes = "CustomTokenScheme")]
[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly IRecordsImportValidationService _importValidationService;
    private readonly IFileIngestionService _fileIngestionService;
    private readonly IStagingReviewService _stagingReviewService;
    private readonly IConfiguration _configuration;

    public IngestionController(
        IRecordsImportValidationService importValidationService,
        IFileIngestionService fileIngestionService,
        IStagingReviewService stagingReviewService,
        IConfiguration configuration)
    {
        _importValidationService = importValidationService;
        _fileIngestionService = fileIngestionService;
        _stagingReviewService = stagingReviewService;
        _configuration = configuration;
    }

    /// <summary>
    /// Accepts an .xlsx upload from the client and validates its column
    /// names and "status" values against category_field_mapping (see
    /// RecordsImportValidationService) before doing anything else with it.
    /// On a validation failure, nothing is ingested anywhere - the caller
    /// gets a 400 with the full issue list back instead so it can show the
    /// user exactly what to fix and re-upload.
    ///
    /// Once validated, ingests the file natively via FileIngestionService.
    /// (n8n is fully decommissioned - this used to also support forwarding
    /// to n8n's webhook behind an Ingestion:UseNativePipeline flag; both the
    /// flag and the n8n client have been removed.)
    /// </summary>

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50 MB
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] int? branchId, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");

        // Required, not inferred - a specific branch has to be selected on
        // the frontend (the "All Branches" pseudo-selection is blocked from
        // uploading client-side for exactly this reason). Every staged row
        // gets tagged with this value (see BulkInsertStaging below), and
        // automation_records_unique is now (category_name, natural_key,
        // branch_id), so there's no sane default to fall back to here.
        if (branchId is null)
            return BadRequest("Select a specific branch before uploading.");

        // Pull the session user id from the JWT token
        var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value
            ?? "unknown";

        // Buffered into a seekable copy because it's read twice: once here
        // by ClosedXML for validation, then again below by
        // FileIngestionService (which also reads it via ClosedXML) -
        // file.OpenReadStream() is forward-only and can't be rewound for a
        // second pass.
        using var buffer = new MemoryStream();
        await using (var uploadStream = file.OpenReadStream())
        {
            await uploadStream.CopyToAsync(buffer, cancellationToken);
        }

        // SuperAdmin uploads every category; a "User" role only gets the
        // categories in their own claim staged/validated - anything else in
        // the file is skipped (not rejected outright), same convention
        // RecordsExportService/InquiryController already use for exports
        // and category-scoped reads.
        var allowedCategories = GetAllowedCategories();

        buffer.Position = 0;
        var validation = await _importValidationService.ValidateAsync(buffer, allowedCategories);
        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        buffer.Position = 0;

        var result = await _fileIngestionService.IngestAsync(buffer, file.FileName, userId, allowedCategories, branchId.Value);

        return Accepted(result);
    }

    /// <summary>
    /// Classifies every staged row for this file (scoped to the caller's own
    /// userid, same as everything else here) as new/already_exist/
    /// mandatory_field/duplicate/dataissue and persists that classification
    /// onto automation_staging.ingest_status.
    /// </summary>
    [HttpGet("review/{fileId}")]
    public async Task<IActionResult> GetReview(string fileId)
    {
        var summary = await _stagingReviewService.ClassifyAsync(fileId, GetUserId());
        if (summary.TotalRows == 0)
        {
            return NotFound($"No staged rows found for file '{fileId}'.");
        }

        return Ok(summary);
    }

    /// <summary>
    /// Re-classifies (idempotent) and returns the same breakdown as a
    /// downloadable .xlsx - one worksheet per category, with
    /// Validationstatus/Detail columns alongside every original field, so
    /// the user can inspect exactly what will and won't be synced before
    /// confirming.
    /// </summary>
    [HttpGet("review/{fileId}/download")]
    public async Task<IActionResult> DownloadReview(string fileId)
    {
        var summary = await _stagingReviewService.ClassifyAsync(fileId, GetUserId());
        if (summary.TotalRows == 0)
        {
            return NotFound($"No staged rows found for file '{fileId}'.");
        }

        var bytes = _stagingReviewService.BuildReviewWorkbook(summary);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"staging-review-{fileId}.xlsx");
    }

    /// <summary>
    /// Deletes every staged row for this file whose last-classified status
    /// isn't "new" or "already_exist" (mandatory_field/duplicate/dataissue),
    /// then promotes the rest into automation_records via
    /// sync_automation_records_all_flows (Config/Queries.json's
    /// Ingestion:SyncStaging key - not sync_automation_staging_ui_for_user,
    /// which is unused). This is the only path that ever calls that
    /// procedure - GetReview/DownloadReview never sync anything by
    /// themselves.
    /// </summary>
    [HttpPost("review/{fileId}/confirm")]
    public async Task<IActionResult> ConfirmReview(string fileId)
    {
        var result = await _stagingReviewService.ConfirmAsync(fileId, GetUserId());
        return Ok(result);
    }

    /// <summary>Same JWT claim IngestionController's upload path already
    /// reads for submittedBy - null if unauthenticated or unparsable, which
    /// automation_staging's userid-scoped queries then simply match nothing
    /// for (fails closed, same as the rest of this controller).</summary>
    private int? GetUserId()
    {
        var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
        return int.TryParse(userId, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Same claim/role logic as InquiryController.GetAllowedCategories -
    /// null means unrestricted (not a restrictable role, or the claim is
    /// empty, same fail-open behavior). Restricts 'User' and 'Admin' role
    /// accounts now (was 'User'-only - see InquiryController.
    /// CheckCategoryAccess's doc comment for why). Duplicated here rather
    /// than shared purely to avoid a cross-controller dependency;
    /// [Authorize(AuthenticationSchemes = "CustomTokenScheme")] on this
    /// controller guarantees User is always an authenticated principal by
    /// the time this runs.
    /// </summary>
    private List<string>? GetAllowedCategories()
    {
        var role = User?.FindFirst(ClaimTypes.Role)?.Value;
        bool isRestrictable = string.Equals(role, "User", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (!isRestrictable)
        {
            return null;
        }

        var categoriesClaim = User?.FindFirst("categories")?.Value;
        if (string.IsNullOrWhiteSpace(categoriesClaim))
        {
            return null;
        }

        return categoriesClaim
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
