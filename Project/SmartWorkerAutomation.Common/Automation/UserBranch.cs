using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class UserBranch
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BranchId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
}