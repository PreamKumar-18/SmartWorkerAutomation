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
//[Authorize]
public class IngestionController : ControllerBase
{
    private readonly N8nIngestionClient _n8nClient;
    private readonly IRecordsImportValidationService _importValidationService;
    private readonly IFileIngestionService _fileIngestionService;
    private readonly IStagingReviewService _stagingReviewService;
    private readonly IConfiguration _configuration;

    public IngestionController(
        N8nIngestionClient n8nClient,
        IRecordsImportValidationService importValidationService,
        IFileIngestionService fileIngestionService,
        IStagingReviewService stagingReviewService,
        IConfiguration configuration)
    {
        _n8nClient = n8nClient;
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
    /// Once validated, either forwards the file to the n8n "Generic
    /// Ingestion (All Categories) webhook" workflow (default) or ingests it
    /// natively via FileIngestionService, depending on
    /// Ingestion:UseNativePipeline in appsettings.json.
    /// </summary>

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50 MB
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are supported.");

        // Pull the session user id from the JWT token
        var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value
            ?? "unknown";

        // Buffered into a seekable copy because it's read twice: once here
        // by ClosedXML for validation, then again below to forward the raw
        // bytes to n8n - file.OpenReadStream() is forward-only and can't be
        // rewound for a second pass.
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

        // Ingestion:UseNativePipeline (default false) - see FileIngestionService
        // for what the native path replicates. Defaults to the n8n forward
        // until the native path has been proven against a real upload; flip
        // the config value (no rebuild needed - Queries.json/appsettings both
        // reload on change) to switch.
        var useNativePipeline = _configuration.GetValue<bool>("Ingestion:UseNativePipeline");

        var result = useNativePipeline
            ? await _fileIngestionService.IngestAsync(buffer, file.FileName, userId, allowedCategories)
            : await _n8nClient.UploadFileAsync(buffer, file.FileName, userId, cancellationToken);

        return Accepted(result);
    }

    /// <summary>
    /// Classifies every staged row for this file (scoped to the caller's own
    /// userid, same as everything else here) as new/already_exist/
    /// mandatory_field/duplicate/dataissue and persists that classification
    /// onto automation_staging.ingest_status. Only meaningful for uploads
    /// that went through the native pipeline (Ingestion:UseNativePipeline) -
    /// n8n-forwarded uploads never leave rows sitting in automation_staging
    /// waiting on this gate.
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
    /// sync_automation_staging_ui_for_user. This is the only path that ever
    /// calls that procedure for a native upload - GetReview/DownloadReview
    /// never sync anything by themselves.
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
    /// null means unrestricted (not a role-"User", or the claim is empty,
    /// same fail-open behavior). Duplicated here rather than shared because
    /// IngestionController currently has no [Authorize] attribute (n8n/
    /// unauthenticated callers can still hit /upload), so this always
    /// degrades to "unrestricted" rather than erroring when there's no
    /// authenticated principal at all - matching this controller's existing
    /// userId resolution (falls back to "unknown", never throws).
    /// </summary>
    private List<string>? GetAllowedCategories()
    {
        bool isUser = string.Equals(User?.FindFirst(ClaimTypes.Role)?.Value, "User", StringComparison.OrdinalIgnoreCase);
        if (!isUser)
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
