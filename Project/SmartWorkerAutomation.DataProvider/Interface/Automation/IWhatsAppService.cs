using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IWhatsAppService
{
    Task<WhatsAppSendResponse> SendAsync(WhatsAppSendRequest request);
}
