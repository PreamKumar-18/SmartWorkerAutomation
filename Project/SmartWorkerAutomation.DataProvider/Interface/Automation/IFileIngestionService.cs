using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IFileIngestionService
{
    /// <summary>
    /// Native replacement for forwarding the upload to n8n's "Generic
    /// Ingestion (All Categories) webhook" - see FileIngestionService for
    /// the full step-by-step. <paramref name="fileStream"/> must be
    /// seekable (the same buffered copy IngestionController already builds
    /// for RecordsImportValidationService). Returns the same shape
    /// N8nIngestionResponse already has so IngestionController and the
    /// frontend don't need to branch on which pipeline ran.
    ///
    /// <paramref name="allowedCategories"/> mirrors the convention
    /// RecordsExportService.ExportAllToExcelAsync already uses: null means
    /// unrestricted (SuperAdmin), otherwise only categories in the set are
    /// actually staged/ingested - a restricted "User" role's file simply
    /// has its other sheets skipped, same as GetAllowedCategories()'s
    /// fail-open convention elsewhere in this codebase.
    /// </summary>
    /// <paramref name="branchId"/> is stamped onto every staged row for this
    /// upload (automation_staging.branch_id, carried through into
    /// automation_records once confirmed) - see BulkInsertStaging in
    /// FileIngestionService and the branch-scoped
    /// automation_records_unique constraint (category_name, natural_key,
    /// branch_id). Required, not optional - IngestionController.UploadFile
    /// rejects the request before this is ever called if the frontend
    /// didn't supply one.
    Task<N8nIngestionResponse> IngestAsync(Stream fileStream, string fileName, string? userId, IReadOnlyCollection<string>? allowedCategories, int branchId);
}
