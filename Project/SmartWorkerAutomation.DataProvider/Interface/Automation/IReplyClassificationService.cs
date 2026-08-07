using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IReplyClassificationService
{
    Task<ReplyClassificationResult> ClassifyAsync(ReplyClassificationInput input, CancellationToken cancellationToken = default);
}
