using SmartWorkerAutomation.Common.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Core;

public class TenantLoginContext
{
    public UserInfo User { get; set; } = null!;
    public Organisation Organisation { get; set; } = null!;
    public string DecryptedConnectionString { get; set; } = string.Empty;
}