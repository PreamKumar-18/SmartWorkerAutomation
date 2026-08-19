using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class Branch
{
    public int Id { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? BranchData { get; set; } // raw JSONB as text; parse/shape as needed later
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
