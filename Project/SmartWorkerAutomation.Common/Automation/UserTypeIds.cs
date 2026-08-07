namespace SmartWorkerAutomation.Common.Automation;

/// <summary>
/// Well-known UserTypeId values, per the userType lookup table
/// (1=SuperAdmin, 2=Admin, 3=User). Kept as named constants so the
/// Admin-creation-rule and category-allowlist checks don't rely on
/// magic numbers scattered across the service layer.
/// </summary>
public static class UserTypeIds
{
    public const int SuperAdmin = 1;
    public const int Admin = 2;
    public const int User = 3;
}
