using System.Text.Json;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Small helpers for reading a Dapper dynamic row (an
/// <see cref="IDictionary{TKey, TValue}"/> keyed by the raw Postgres column
/// name, e.g. "rule_name") by that column name. This codebase doesn't
/// configure a snake_case-to-PascalCase Dapper type map, so POCO
/// auto-mapping isn't available for these hand-written queries - every
/// dynamic query result gets consumed through these instead. Originally
/// written for NotificationsService (the manual single-id send path) and
/// shared with ReminderSendBackgroundService (the scheduled batch send),
/// since both read the identical column set
/// fn_get_automation_notification_by_id/fn_get_pending_automation_notifications
/// return.
///
/// Public (not internal) because it's used both from within this assembly
/// (NotificationsService) and from SmartWorker.API's
/// ReminderSendBackgroundService, a separate assembly.
/// </summary>
public static class DynamicRowExtensions
{
    public static string? GetString(this IDictionary<string, object> fields, string key)
        => fields.TryGetValue(key, out var value) && value is not null && value != DBNull.Value
            ? value.ToString()
            : null;

    public static bool GetBool(this IDictionary<string, object> fields, string key)
        => fields.TryGetValue(key, out var value) && value is bool flag && flag;

    public static JsonElement? GetJsonElement(this IDictionary<string, object> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value) || value is null || value == DBNull.Value)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element;
        }

        var raw = value.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
