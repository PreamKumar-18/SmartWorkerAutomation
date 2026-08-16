namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Result of a Records drawer Call action - PhoneNumber is empty when the
/// record has no phone on file at all; AutoDialTriggered is true only when
/// an Android device with a registered push token was found for the calling
/// user (see InquiryService.InitiateCallAsync). The frontend falls back to a
/// `tel:` link (mobile only - web never does, see records-page.component.ts's
/// own doc comment) when AutoDialTriggered is false but PhoneNumber isn't
/// empty.
/// </summary>
public class CallInitiationResult
{
    public string PhoneNumber { get; set; } = string.Empty;

    public bool AutoDialTriggered { get; set; }
}
