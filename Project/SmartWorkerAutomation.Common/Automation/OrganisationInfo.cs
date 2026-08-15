using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class OrganisationInfo
{
    public int Id { get; set; }
    public int OrgId { get; set; }
    public string DbName { get; set; }
    public string ConnectionString { get; set; } = string.Empty; // encrypted at rest
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
