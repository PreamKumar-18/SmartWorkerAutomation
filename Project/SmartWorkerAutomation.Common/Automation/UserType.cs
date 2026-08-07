using System;

namespace SmartWorkerAutomation.Common.Automation;

public class UserType
{
    public int UserTypeId { get; set; }
    public string UserTypeVal { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
