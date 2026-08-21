using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class OrganisationSummary
{
    public int OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyDetails { get; set; }
}
