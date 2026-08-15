using System;
using System.ComponentModel.DataAnnotations;

namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Records drawer's Promise to pay section ("Promised amount" / "Promised
/// by" - see RecordDetailDrawerComponent + buildPromiseRows in
/// record-drawer-metrics.util.ts) - both fields are nullable so either can
/// be cleared independently (e.g. correcting a promise that turned out to be
/// wrong without needing to also re-supply the other value). Not part of the
/// generic PATCH /api/Inquiry/{id} edit endpoint's per-category `changes`
/// payload - promised_amount/snooze_until live on automation_records, not
/// any category's own business_data table, so they're written via
/// InquiryService.UpdatePromiseToPayAsync -> update_promise_to_pay(...)
/// instead (see Database/update_promise_to_pay.sql).
/// </summary>
public class UpdatePromiseToPayRequest
{
    [Required]
    public int Id { get; set; }

    public decimal? PromisedAmount { get; set; }

    public DateTime? PromisedBy { get; set; }
}
