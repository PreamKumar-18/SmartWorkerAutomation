using System;

namespace SmartWorkerAutomation.Common.Automation;

public class User
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public int? UserTypeId { get; set; }
    public string? RoleName { get; set; }
    /// <summary>
    /// Category allowlist for 'User'-role accounts (e.g. ["finance","purchase"]).
    /// Null/empty for Admin and SuperAdmin - they're never checked against this,
    /// they always see every category.
    /// </summary>
    public string[]? AllowedCategories { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
