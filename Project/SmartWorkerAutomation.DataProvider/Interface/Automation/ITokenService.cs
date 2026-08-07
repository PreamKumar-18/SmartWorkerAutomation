using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface ITokenService
{
    string GenerateToken(User user);
}
