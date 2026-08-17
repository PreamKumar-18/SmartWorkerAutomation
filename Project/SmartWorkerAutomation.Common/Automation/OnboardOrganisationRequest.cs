using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.Common.Automation;

public class OnboardOrganisationRequest
{
    public string OrganisationName { get; set; } = string.Empty;
    public string? CompanyDetailsJson { get; set; }
    public string? DbName { get; set; }
    public string TenantConnectionString { get; set; } = string.Empty; // plaintext in, encrypted before storage
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string? AdminPhone { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty; // plaintext in, hashed before storage
    public int RoleId { get; set; }        // NEW
    public int AccessTypeId { get; set; }
    public string[]? AdminAllowedCategories { get; set; }
    public string? WebhookPhoneNumber { get; set; }
}


public class OnboardOrganisationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? OrgId { get; set; }
}
