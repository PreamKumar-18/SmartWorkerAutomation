using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.DataProvider.Service.Automation;


namespace SmartWorkerAutomation.DataProvider.Automation;

public interface ITokenService
{
    string GenerateToken(User user, UserInfo masterUserInfo, IEnumerable<UserBranchSummary>? branches);
}
