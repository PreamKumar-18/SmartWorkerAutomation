using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class Organisation
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyDetails { get; set; } // JSONB as raw text; parse/shape as needed later
    public bool Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}