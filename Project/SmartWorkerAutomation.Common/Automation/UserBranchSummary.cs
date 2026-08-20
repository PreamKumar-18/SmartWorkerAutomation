using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class UserBranchSummary
{
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}