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
    public string? WebhookPhoneNumber { get; set; } // doubles as the outbound Meta phone_number_id

    // All nullable/optional - null means "use the global Meta:*/Smtp:*
    // config" (see TenantResolverService.GetWhatsAppCredentialsAsync /
    // GetSmtpCredentialsAsync). WhatsAppAccessToken and SmtpPassword are
    // encrypted at rest, same as ConnectionString.
    public string? WhatsAppAccessToken { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }
}
