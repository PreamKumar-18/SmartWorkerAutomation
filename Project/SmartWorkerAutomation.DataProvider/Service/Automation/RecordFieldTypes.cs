using System;
using System.Collections.Generic;

namespace SmartWorkerAutomation.DataProvider.Automation;

/// <summary>
/// Per-category business_data_key -> expected Postgres type. Originally
/// InquiryService's own private EditableFields dictionary (server-side
/// mirror of each category's editable RecordFieldDef entries in the
/// frontend's records-field-schema.ts) - extracted here so
/// StagingReviewService can reuse the exact same type expectations for its
/// "dataissue" datatype check on freshly-staged rows, instead of
/// maintaining a second, possibly-drifting copy.
///
/// Only fields with a non-"text" PgType are meaningfully checkable here -
/// any text value is valid text, so StagingReviewService only runs a parse
/// check for numeric/int/date/bool entries.
/// </summary>
public static class RecordFieldTypes
{
    public static readonly IReadOnlyDictionary<string, (string Key, string PgType)[]> ByCategory = new Dictionary<string, (string Key, string PgType)[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["finance"] = new[]
        {
            ("client_priority", "text"),
            ("invoice_amount", "numeric"),
            ("invoice_date", "date"),
            ("credit_days", "int"),
            ("payment_status", "text"),
            ("paid_amount", "numeric"),
            ("payment_date", "date"),
            ("remaining_days_payment", "int"),
            ("pause_reminders", "bool"),
        },
        ["inventory"] = new[]
        {
            ("item_name", "text"),
            ("current_stock", "numeric"),
            ("reorder_point", "numeric"),
            ("stock_status", "text"),
            ("warehouse_location", "text"),
            ("procurement_email", "text"),
            ("procurement_phone", "text"),
            ("pause_reminders", "bool"),
        },
        ["dispatch"] = new[]
        {
            ("dispatch_type", "text"),
            ("item_name", "text"),
            ("quantity", "numeric"),
            ("dispatch_status", "text"),
            ("planned_dispatch_date", "date"),
            ("actual_dispatch_date", "date"),
            ("delivery_date", "date"),
            ("customer_email", "text"),
            ("customer_phone", "text"),
            ("logistics_email", "text"),
            ("logistics_phone", "text"),
            ("pause_reminders", "bool"),
        },
        ["purchase"] = new[]
        {
            ("expected_days", "int"),
            ("material_status", "text"),
            ("item_description", "text"),
            ("quantity", "numeric"),
            ("unit_price", "numeric"),
            ("order_date", "date"),
            ("expected_date", "date"),
            ("delivery_date", "date"),
            // grn_date - see Database/add_purchase_grn_date.sql. Must be
            // deployed together with that migration and the matching
            // @p_grn_date addition to Config/Queries.json's "UpdatePurchase"
            // entry - this array drives which p_xxx parameters get built for
            // the update_purchase_record() call, so it has to match that
            // function's actual argument list exactly (both in presence and
            // position) or the call fails outright for every Purchase edit,
            // not just this field.
            ("grn_date", "date"),
            ("pause_reminders", "bool"),
        },
        ["production"] = new[]
        {
            ("item_name", "text"),
            ("quantity", "numeric"),
            ("production_type", "text"),
            ("production_status", "text"),
            ("planned_start_date", "date"),
            ("planned_completion_date", "date"),
            ("actual_start_date", "date"),
            ("actual_completion_date", "date"),
            ("supervisor_email", "text"),
            ("supervisor_phone", "text"),
            ("vendor_email", "text"),
            ("vendor_phone", "text"),
            ("pause_reminders", "bool"),
        },
    };
}
