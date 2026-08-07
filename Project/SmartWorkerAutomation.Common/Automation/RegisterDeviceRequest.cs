namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Sent right after a successful login (web or mobile) so the backend can
/// push notifications to this device later. DeviceId should be stable
/// across app restarts on the same device/browser (native: platform device
/// id; web: a UUID generated once and kept in localStorage) so repeat
/// logins from the same device update the existing row instead of piling
/// up duplicates.
/// </summary>
public class RegisterDeviceRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string PushToken { get; set; } = string.Empty;

    /// <summary>"android" | "ios" | "web".</summary>
    public string Platform { get; set; } = string.Empty;

    public string? DeviceModel { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
}
