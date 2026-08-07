using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IRecordsImportValidationService
{
    /// <summary>
    /// Validates an uploaded .xlsx's column names and "status" values before
    /// it's ingested - see RecordsImportValidationService for the rules.
    /// <paramref name="fileStream"/> must be seekable (a MemoryStream copy
    /// of the upload, not the raw IFormFile stream, since ClosedXML needs
    /// to read it and the caller still needs to forward/ingest the original
    /// bytes afterward).
    ///
    /// <paramref name="allowedCategories"/> follows the same convention as
    /// RecordsExportService/FileIngestionService: null means unrestricted
    /// (SuperAdmin), otherwise sheets for categories outside the set are
    /// skipped entirely (flagged as a warning, not validated column-by-
    /// column) - a restricted "User" shouldn't see confusing errors about a
    /// category they can't access anyway, since FileIngestionService will
    /// silently skip it during actual ingestion regardless.
    /// </summary>
    Task<ImportValidationResult> ValidateAsync(Stream fileStream, IReadOnlyCollection<string>? allowedCategories);
}
