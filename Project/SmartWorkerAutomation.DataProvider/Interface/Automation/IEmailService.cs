using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IEmailService
{
    Task<EmailSendResponse> SendAsync(EmailSendRequest request);
}
