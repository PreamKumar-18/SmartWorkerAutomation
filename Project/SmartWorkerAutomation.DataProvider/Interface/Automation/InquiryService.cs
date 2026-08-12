using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class InquiryService : IInquiryService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;

    private static readonly Dictionary<string, string> CategoryToViewMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "dispatch", "public.dispatch_view" },
        { "finance", "public.finance_view" },
        { "inventory", "public.inventory_view" },
        { "production", "public.production_view" },
        { "purchase", "public.purchase_view" },
        { "filetracking", "public.file_tracking" },
        { "ruleconfiguration", "public.rule_configuration_view" }
    };

    /// <summary>
    /// Server-side mirror of each category's editable RecordFieldDef entries
    /// in the frontend's records-field-schema.ts (editable: true only) - the
    /// allowlist that decides which keys in an UpdateRecordAsync `changes`
    /// payload are ever written. PgType drives both the JSON->CLR conversion
    /// below and which update_{category}_record(...) Postgres function
    /// parameter it lines up with in Queries.json - keep the three in sync
    /// if a field is ever added/removed from the schema.
    ///
    /// Extracted to Services/RecordFieldTypes.cs so StagingReviewService can
    /// reuse the exact same per-category type map for its "dataissue"
    /// datatype check instead of duplicating it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Key, string PgType)[]> EditableFields = RecordFieldTypes.ByCategory;

    // Categories that hold global/admin-managed configuration data rather than
    // per-user records. These are never filtered by userid, even for non-superadmins.
    private static readonly HashSet<string> GlobalCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "ruleconfiguration"
    };

    /// <summary>
    /// Categories that support the quick single-field status action, and
    /// which Queries.json entry backs each one. See
    /// Database/add_status_quick_actions.sql for the underlying
    /// update_finance_payment_status/update_purchase_material_status
    /// functions - each one hardcodes its own single allowed forward
    /// transition (or transition pair, for Purchase), so p_status here is
    /// really just "the transition to attempt," validated against the
    /// record's actual current status inside the function itself.
    /// </summary>
    private static readonly Dictionary<string, string> StatusUpdateQueryByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        { "finance", "Inquiry:UpdateFinancePaymentStatus" },
        { "purchase", "Inquiry:UpdatePurchaseMaterialStatus" },
    };

    /// <summary>
    /// Npgsql can't always infer a null parameter's Postgres type from
    /// context alone when calling a function (raises "could not determine
    /// data type of parameter") - passing DbType explicitly here, for every
    /// parameter, sidesteps that regardless of whether the value ends up
    /// null.
    /// </summary>
    private static readonly Dictionary<string, DbType> PgTypeToDbType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = DbType.String,
        ["int"] = DbType.Int32,
        ["numeric"] = DbType.Decimal,
        ["bool"] = DbType.Boolean,
        ["date"] = DbType.Date,
    };

    public InquiryService(DbConnectionFactory connectionFactory, IQueryStore queryStore)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
    }

    public async Task<IEnumerable<dynamic>> GetInquiryDataAsync(string category, string userIdClaim, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        if (!CategoryToViewMap.TryGetValue(category.Trim(), out var viewName))
        {
            throw new ArgumentException($"Invalid category: '{category}'. Allowed categories are: {string.Join(", ", CategoryToViewMap.Keys)}");
        }

        using var connection = _connectionFactory.CreateConnection();
        bool isPurchase = category.Trim() == "purchase";
        string queryKey = isPurchase ? "Inquiry:GetAllWithJoin" : "Inquiry:GetAll";

        if (isSuperAdmin || GlobalCategories.Contains(category.Trim()))
        {
            var sql = _queryStore.Render(queryKey, new Dictionary<string, string> { ["ViewName"] = viewName });
            return await connection.QueryAsync(sql);
        }
        else
        {
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token.");
            }

            var sql = _queryStore.Render("Inquiry:GetByUser", new Dictionary<string, string> { ["ViewName"] = viewName });
            return await connection.QueryAsync(sql, new { UserId = userId });
        }
    }

    public async Task<bool> UpdateFileStatusAsync(int id, string status, string userIdClaim, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status is required.");
        }

        int userId = 0;
        if (!isSuperAdmin)
        {
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token.");
            }
        }

        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Inquiry:UpdateFileStatus");
        return await connection.ExecuteScalarAsync<bool>(sql, new
        {
            p_id = id,
            p_status = status,
            p_userid = userId,
            p_is_superadmin = isSuperAdmin
        });
    }

    public async Task<dynamic?> UpdateRecordAsync(string category, int id, Dictionary<string, JsonElement> changes, string userIdClaim, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        var normalizedCategory = category.Trim().ToLowerInvariant();

        if (!EditableFields.TryGetValue(normalizedCategory, out var fields))
        {
            throw new ArgumentException($"Category '{category}' does not support record edits.");
        }

        if (!CategoryToViewMap.TryGetValue(normalizedCategory, out var viewName))
        {
            throw new ArgumentException($"Invalid category: '{category}'.");
        }

        int userId = 0;
        if (!isSuperAdmin)
        {
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token.");
            }
        }

        var parameters = new DynamicParameters();
        parameters.Add("p_id", id, DbType.Int32);
        foreach (var (key, pgType) in fields)
        {
            parameters.Add("p_" + key, ConvertValue(changes, key, pgType), PgTypeToDbType[pgType]);
        }
        parameters.Add("p_userid", userId, DbType.Int32);
        parameters.Add("p_is_superadmin", isSuperAdmin, DbType.Boolean);

        using var connection = _connectionFactory.CreateConnection();

        var updateSql = _queryStore.Get($"Inquiry:Update{Capitalize(normalizedCategory)}");
        var updated = await connection.ExecuteScalarAsync<bool>(updateSql, parameters);
        if (!updated)
        {
            return null;
        }
        await connection.ExecuteAsync(_queryStore.Get("Inquiry:Updateautomationrecordlogic"), new { p_id = id, p_category = Capitalize(normalizedCategory) });
        var fetchSql = _queryStore.Render("Inquiry:GetById", new Dictionary<string, string> { ["ViewName"] = viewName });
        return await connection.QuerySingleOrDefaultAsync(fetchSql, new { Id = id });
    }

    public async Task<dynamic?> UpdateRecordStatusAsync(string category, int id, string newStatus, string userIdClaim, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        if (string.IsNullOrWhiteSpace(newStatus))
        {
            throw new ArgumentException("Status is required.");
        }

        var normalizedCategory = category.Trim().ToLowerInvariant();

        if (!StatusUpdateQueryByCategory.TryGetValue(normalizedCategory, out var queryKey))
        {
            throw new ArgumentException($"Category '{category}' does not support status actions.");
        }

        if (!CategoryToViewMap.TryGetValue(normalizedCategory, out var viewName))
        {
            throw new ArgumentException($"Invalid category: '{category}'.");
        }

        int userId = 0;
        if (!isSuperAdmin)
        {
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token.");
            }
        }

        using var connection = _connectionFactory.CreateConnection();

        bool updated;
        try
        {
            updated = await connection.ExecuteScalarAsync<bool>(_queryStore.Get(queryKey), new
            {
                p_id = id,
                p_status = newStatus.Trim(),
                p_userid = userId,
                p_is_superadmin = isSuperAdmin
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.RaiseException)
        {
            // RAISE EXCEPTION from update_finance_payment_status /
            // update_purchase_material_status - invalid target status, or
            // the record's current status doesn't allow this transition.
            // Surface as a 400, not a 500.
            throw new ArgumentException(ex.MessageText);
        }

        if (!updated)
        {
            return null;
        }

        await connection.ExecuteAsync(_queryStore.Get("Inquiry:Updateautomationrecordlogic"), new { p_id = id, p_category = Capitalize(normalizedCategory) });
        var fetchSql = _queryStore.Render("Inquiry:GetById", new Dictionary<string, string> { ["ViewName"] = viewName });
        return await connection.QuerySingleOrDefaultAsync(fetchSql, new { Id = id });
    }

    private static string Capitalize(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);

    /// <summary>
    /// Converts one field's raw JSON value (as sent by RecordEditDialogComponent's
    /// save(), which always includes every editable field - not a partial diff)
    /// into the CLR type Dapper needs for that column. Missing/JSON-null values
    /// become a real null parameter rather than erroring, so a field the dialog
    /// left blank clears the column instead of failing the whole update.
    /// </summary>
    private static object? ConvertValue(Dictionary<string, JsonElement> changes, string key, string pgType)
    {
        if (!changes.TryGetValue(key, out var element) ||
            element.ValueKind == JsonValueKind.Null ||
            element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        switch (pgType)
        {
            case "bool":
                return element.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(element.GetString(), out var b) ? b : (object?)null,
                    _ => null,
                };
            case "int":
                return element.ValueKind switch
                {
                    JsonValueKind.Number => element.TryGetInt32(out var i) ? i : (object?)null,
                    JsonValueKind.String => int.TryParse(element.GetString(), out var iStr) ? iStr : (object?)null,
                    _ => null,
                };
            case "numeric":
                return element.ValueKind switch
                {
                    JsonValueKind.Number => element.TryGetDecimal(out var d) ? d : (object?)null,
                    JsonValueKind.String => decimal.TryParse(element.GetString(), out var dStr) ? dStr : (object?)null,
                    _ => null,
                };
            case "date":
                var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
                return !string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out var dt) ? dt : (object?)null;
            default: // text
                return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }
    }
}
