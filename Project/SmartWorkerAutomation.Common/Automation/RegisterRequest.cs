namespace SmartWorkerAutomation.Common.Automation;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public int UserTypeId { get; set; }
    /// <summary>
    /// Category allowlist - only meaningful/persisted when UserTypeId is the
    /// 'User' role; ignored for Admin/SuperAdmin.
    /// </summary>
    public string[]? AllowedCategories { get; set; }

    //public int RoleId { get; set; }
    //public int AccessTypeId { get; set; }

    public int OrgId { get; set; }

    public int[]? BranchIds { get; set; }      // NEW
    public int? PrimaryBranchId { get; set; }
}
