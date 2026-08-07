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
}
