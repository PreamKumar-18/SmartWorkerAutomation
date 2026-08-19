using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class InquiryService : IInquiryService
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly IQueryStore _queryStore;
    private readonly IFirebasePushService _pushService;

    /// <summary>
    /// Which field on each category's row holds the phone number to call -
    /// Finance-only for the initial rollout (matches the frontend's
    /// CALL_ENABLED_CATEGORIES in record-drawer-metrics.util.ts /
    /// record-actions.util.ts). Extend both allowlists together when adding
    /// a category, or the button would show up client-side and 400 against
    /// this backend.
    /// </summary>
    private static readonly Dictionary<string, string> PhoneFieldByCategory = new(StringComparer.OrdinalIgnoreCase)
{
    { "finance", "client_phone" },
    { "purchase", "supplier_phone" },
    { "inventory", "procurement_phone" },
    { "dispatch", "customer_phone" },
    { "production", "supervisor_phone" },
};

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

    public InquiryService(DbConnectionFactory connectionFactory, IQueryStore queryStore, IFirebasePushService pushService)
    {
        _connectionFactory = connectionFactory;
        _queryStore = queryStore;
        _pushService = pushService;
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

    public async Task<dynamic?> GetRecordByIdAsync(string category, int id, string userIdClaim, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        var normalizedCategory = category.Trim();

        if (!CategoryToViewMap.TryGetValue(normalizedCategory, out var viewName))
        {
            throw new ArgumentException($"Invalid category: '{category}'. Allowed categories are: {string.Join(", ", CategoryToViewMap.Keys)}");
        }

        using var connection = _connectionFactory.CreateConnection();

        if (isSuperAdmin || GlobalCategories.Contains(normalizedCategory))
        {
            var sql = _queryStore.Render("Inquiry:GetById", new Dictionary<string, string> { ["ViewName"] = viewName });
            return await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });
        }
        else
        {
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token.");
            }

            var sql = _queryStore.Render("Inquiry:GetByIdForUser", new Dictionary<string, string> { ["ViewName"] = viewName });
            return await connection.QuerySingleOrDefaultAsync(sql, new { Id = id, UserId = userId });
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

    /// <summary>
    /// Records drawer's Promise to pay section - see the interface's own
    /// doc comment. No category lookup at all (unlike UpdateRecordAsync/
    /// UpdateRecordStatusAsync) since promised_amount/snooze_until live on
    /// automation_records directly, keyed only by id - same shape as
    /// UpdateFileStatusAsync just above.
    /// </summary>
    public async Task<bool> UpdatePromiseToPayAsync(int id, decimal? promisedAmount, DateTime? promisedBy, string userIdClaim, bool isSuperAdmin)
    {
        int userId = 0;
        if (!isSuperAdmin)
        {
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID in token.");
            }
        }

        using var connection = _connectionFactory.CreateConnection();
        var sql = _queryStore.Get("Inquiry:UpdatePromiseToPay");
        return await connection.ExecuteScalarAsync<bool>(sql, new
        {
            p_id = id,
            p_promised_amount = promisedAmount,
            p_promised_by = promisedBy,
            p_userid = userId,
            p_is_superadmin = isSuperAdmin
        });
    }

    public async Task<CallInitiationResult?> InitiateCallAsync(string category, int id, string userIdClaim, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.");
        }

        var normalizedCategory = category.Trim();
        if (!PhoneFieldByCategory.TryGetValue(normalizedCategory, out var phoneField))
        {
            throw new ArgumentException($"Category '{category}' does not support calling yet.");
        }

        var row = await GetRecordByIdAsync(normalizedCategory, id, userIdClaim, isSuperAdmin);
        if (row is null)
        {
            return null;
        }

        var fields = (IDictionary<string, object>)row;
        var phoneNumber = fields.GetString(phoneField);
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new CallInitiationResult { PhoneNumber = string.Empty, AutoDialTriggered = false };
        }

        var result = new CallInitiationResult { PhoneNumber = phoneNumber, AutoDialTriggered = false };

        // The device to auto-dial from is always the *calling* user's own
        // device (whoever is currently logged in and tapped Call), never the
        // record's assigned owner's device - those are two different people.
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int callingUserId))
        {
            return result;
        }

        using var connection = _connectionFactory.CreateConnection();
        var deviceSql = _queryStore.Get("Inquiry:GetUserDeviceForCall");
        var device = await connection.QuerySingleOrDefaultAsync(deviceSql, new { UserId = callingUserId });
        if (device is null)
        {
            return result;
        }

        var deviceFields = (IDictionary<string, object>)device;
        var platform = deviceFields.GetString("platform");
        var pushToken = deviceFields.GetString("push_token");

        if (!string.Equals(platform, "android", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(pushToken))
        {
            return result;
        }

        try
        {
            var accessToken = await _pushService.GetAccessTokenAsync();
            await _pushService.SendDataOnlyAsync(
                accessToken,
                pushToken,
                new Dictionary<string, string> { ["type"] = "auto_dial", ["phone_number"] = phoneNumber });
            result.AutoDialTriggered = true;
        }
        catch
        {
            // Non-fatal - the frontend falls back to a tel: link (mobile) or
            // shows the number (web) when AutoDialTriggered is false.
        }

        return result;
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

        using var connection = _connectionFactory.CreateConnection();

        // CRITICAL: update_{category}_record(...) has a fixed signature -
        // every field in `fields` is sent as a parameter on every call, even
        // when the caller only means to change one of them (e.g. the drawer's
        // Pause Reminders/Reminder toggle, which only ever sends
        // { pause_reminders }). Previously, any field absent from `changes`
        // fell through ConvertValue's "key not present -> null" branch and
        // was sent as SQL NULL, and since the Postgres function does an
        // unconditional UPDATE ... SET on every one of its params, that
        // silently wiped every OTHER editable column back to null - this is
        // what emptied client_priority/invoice_amount/invoice_date/
        // credit_days/payment_status (and, as a knock-on effect, the view's
        // computed due_datecal/day_overdue) on rows where only the Reminder
        // toggle (or any other partial-changes caller) was ever touched.
        // Fetching the current row first and falling back to its existing
        // value for any key `changes` doesn't include fixes this without
        // touching the update_*_record function bodies themselves (which
        // aren't checked into this repo - see Database/ comments elsewhere).
        var currentFetchSqlForMerge = _queryStore.Render("Inquiry:GetById", new Dictionary<string, string> { ["ViewName"] = viewName });
        var currentRow = await connection.QuerySingleOrDefaultAsync(currentFetchSqlForMerge, new { Id = id }) as IDictionary<string, object>;

        var parameters = new DynamicParameters();
        parameters.Add("p_id", id, DbType.Int32);
        foreach (var (key, pgType) in fields)
        {
            object? value = changes.ContainsKey(key)
                ? ConvertValue(changes, key, pgType)
                : (currentRow != null && currentRow.TryGetValue(key, out var existing) ? existing : null);
            parameters.Add("p_" + key, value, PgTypeToDbType[pgType]);
        }
        parameters.Add("p_userid", userId, DbType.Int32);
        parameters.Add("p_is_superadmin", isSuperAdmin, DbType.Boolean);

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
