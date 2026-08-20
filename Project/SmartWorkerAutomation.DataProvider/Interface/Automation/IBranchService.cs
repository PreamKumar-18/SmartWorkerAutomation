using SmartWorkerAutomation.Common.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Interface.Automation;

public interface IBranchService
{
    Task<IEnumerable<UserBranchSummary>> GetBranchesForUserAsync(int userId);
}
