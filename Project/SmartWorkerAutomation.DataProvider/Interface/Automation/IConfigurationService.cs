using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IConfigurationService
{
    Task<bool> UpdateRuleAlertConfigAsync(UpdateRuleAlertConfigRequest request);
    Task<bool> UpdateEmailTemplateConfigAsync(UpdateEmailTemplateConfigRequest request);
}
