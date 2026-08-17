using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Dapper;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Standalone Customer Enquiry bulk-import path - see
/// ICustomerEnquiryImportService for why this is a wholly separate service
/// from CustomerEnquiryService rather than new methods bolted onto it.
///
/// Expected header row (order doesn't matter, matched by name,
/// case-insensitive): Name, Customer Name, Mailing Street, Mailing City,
/// Mailing State/Province, Mailing Zip/Postal Code, Phone, Email. "Name" is
/// the contact person (contact_name); "Customer Name" is the
/// company/account (customer_name, required - rows with it blank are
/// skipped as invalid, since the column is NOT NULL on the table).
///
/// Duplicate detection key is (contact_name, customer_name), trimmed and
/// compared case-insensitively - matches how the source spreadsheet is
/// actually structured (many rows share a customer_name with different
/// contacts at the same company, so customer_name alone would falsely
/// collide). A row whose key already exists in customer_enquiries, or
/// duplicates an earlier row in the same file, is skipped rather than
/// inserted - never updates/overwrites an existing row.
/// </summary>
public class CustomerEnquiryImportService : ICustomerEnquiryImportService
{
    private static readonly string[] TemplateHeaders =
    {
        "Name", "Customer Name", "Mailing Street", "Mailing City",
        "Mailing State/Province", "Mailing Zip/Postal Code", "Phone", "Email"
    };

    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    public CustomerEnquiryImportService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public byte[] BuildTemplateWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customer Enquiries");

        for (var col = 0; col < TemplateHeaders.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = TemplateHeaders[col];
            cell.Style.Font.Bold = true;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<CustomerEnquiryImportResult> ImportAsync(Stream fileStream, string fileName, string? importedBy)
    {
        var rows = ParseRows(fileStream, fileName);

        var result = new CustomerEnquiryImportResult { TotalRows = rows.Count };

        using var connection = _connectionFactory.CreateConnection();

        var existingKeysSql = _queryStore.Get("CustomerEnquiry:GetExistingKeys");
        var existingRows = await connection.QueryAsync<(string ContactName, string CustomerName)>(existingKeysSql);
        var seenKeys = new HashSet<string>(
            existingRows.Select(r => BuildKey(r.ContactName, r.CustomerName)),
            StringComparer.OrdinalIgnoreCase);

        var insertSql = _queryStore.Get("CustomerEnquiry:Insert");

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CustomerName))
            {
                result.SkippedInvalid++;
                result.Issues.Add(new CustomerEnquiryImportRowIssue
                {
                    RowNumber = row.RowNumber,
                    Reason = "'Customer Name' is required and was blank."
                });
                continue;
            }

            var key = BuildKey(row.ContactName, row.CustomerName);
            if (!seenKeys.Add(key))
            {
                result.SkippedDuplicates++;
                continue;
            }

            await connection.ExecuteAsync(insertSql, new
            {
                ContactName = NullIfBlank(row.ContactName),
                CustomerName = row.CustomerName.Trim(),
                MailingStreet = NullIfBlank(row.MailingStreet),
                MailingCity = NullIfBlank(row.MailingCity),
                MailingState = NullIfBlank(row.MailingState),
                MailingZip = NullIfBlank(row.MailingZip),
                Phone = NullIfBlank(row.Phone),
                Email = NullIfBlank(row.Email),
                EnquiryStatus = CustomerEnquiryStatus.NotContacted,
                Remarks = (string?)null,
                CreatedBy = importedBy
            });

            result.Inserted++;
        }

        return result;
    }

    private static string BuildKey(string? contactName, string? customerName) =>
        $"{(contactName ?? string.Empty).Trim()}|{(customerName ?? string.Empty).Trim()}";

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private record ImportRow(
        int RowNumber, string? ContactName, string CustomerName, string? MailingStreet,
        string? MailingCity, string? MailingState, string? MailingZip, string? Phone, string? Email);

    private static List<ImportRow> ParseRows(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ParseCsv(fileStream),
            _ => ParseExcel(fileStream), // .xlsx/.xls both go through ClosedXML
        };
    }

    private static List<ImportRow> ParseExcel(Stream fileStream)
    {
        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.Row(1);
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var columnIndex = MapHeaderColumns(col => headerRow.Cell(col).GetString(), lastCol);

        var rows = new List<ImportRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= lastRow; r++)
        {
            string Get(string header) =>
                columnIndex.TryGetValue(header, out var col) ? sheet.Cell(r, col).GetString().Trim() : string.Empty;

            if (lastCol > 0 && Enumerable.Range(1, lastCol).All(c => sheet.Cell(r, c).GetString().Trim().Length == 0))
            {
                continue; // fully blank row
            }

            rows.Add(new ImportRow(
                r, Get("Name"), Get("Customer Name"), Get("Mailing Street"), Get("Mailing City"),
                Get("Mailing State/Province"), Get("Mailing Zip/Postal Code"), Get("Phone"), Get("Email")));
        }

        return rows;
    }

    private static List<ImportRow> ParseCsv(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            return new List<ImportRow>();
        }

        var header = SplitCsvLine(lines[0]);
        var columnIndex = MapHeaderColumns(col => col <= header.Count ? header[col - 1] : string.Empty, header.Count);

        var rows = new List<ImportRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = SplitCsvLine(lines[i]);

            string Get(string headerName) =>
                columnIndex.TryGetValue(headerName, out var col) && col <= cells.Count
                    ? cells[col - 1].Trim()
                    : string.Empty;

            rows.Add(new ImportRow(
                i + 1, Get("Name"), Get("Customer Name"), Get("Mailing Street"), Get("Mailing City"),
                Get("Mailing State/Province"), Get("Mailing Zip/Postal Code"), Get("Phone"), Get("Email")));
        }

        return rows;
    }

    /// <summary>1-based column index (matching both ClosedXML's Cell(row,col)
    /// and the CSV splitter's 1-based Get(col)) per recognized header name,
    /// case-insensitive, "Zip"/"Zip Code"/"Postal Code" tolerated as aliases
    /// for "Mailing Zip/Postal Code" since spreadsheet exports vary.</summary>
    private static Dictionary<string, int> MapHeaderColumns(Func<int, string> cellAt, int lastCol)
    {
        var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = new[] { "Name", "Contact Name" },
            ["Customer Name"] = new[] { "Customer Name", "Customer Nam", "Account Name", "Company" },
            ["Mailing Street"] = new[] { "Mailing Street", "Street" },
            ["Mailing City"] = new[] { "Mailing City", "City" },
            ["Mailing State/Province"] = new[] { "Mailing State/Province", "State", "Mailing State" },
            ["Mailing Zip/Postal Code"] = new[] { "Mailing Zip/Postal Code", "Zip", "Zip Code", "Postal Code", "Mailing Zip" },
            ["Phone"] = new[] { "Phone" },
            ["Email"] = new[] { "Email" },
        };

        var result = new Dictionary<string, int>();
        for (var col = 1; col <= lastCol; col++)
        {
            var text = cellAt(col).Trim();
            if (text.Length == 0) continue;

            foreach (var (canonical, names) in aliases)
            {
                if (result.ContainsKey(canonical)) continue;
                if (names.Any(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase)))
                {
                    result[canonical] = col;
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>Minimal RFC4180-ish splitter - handles double-quoted fields
    /// (including embedded commas/escaped "" quotes) since Excel's own "Save
    /// As CSV" quotes any field containing a comma.</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
