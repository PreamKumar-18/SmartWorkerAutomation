namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IFirebasePushService
{
    /// <summary>
    /// Signs a fresh service-account JWT and exchanges it for a Google
    /// OAuth access token good for ~1 hour - callers should fetch this
    /// once per batch and reuse it across every SendAsync call in that
    /// batch, not once per push.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task SendAsync(
        string accessToken,
        string pushToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Data-only push (no `notification` block) - deliberately used for the
    /// Records drawer Call action's auto-dial signal instead of SendAsync
    /// above. A message that includes a `notification` block gets
    /// intercepted by Android's system tray whenever the app is backgrounded
    /// or killed and never reaches app code until the user taps it, which
    /// would make "auto"-dial only actually work while the app happens to be
    /// open. A pure data message is always delivered straight to app code
    /// (subject to OS/vendor battery-optimization limits), so the app itself
    /// shows its own "Calling now" local notification instead (see mobile's
    /// AutoDialPlugin.showCallingNotification).
    /// </summary>
    Task SendDataOnlyAsync(
        string accessToken,
        string pushToken,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}
