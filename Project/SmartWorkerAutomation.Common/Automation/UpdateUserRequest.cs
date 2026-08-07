namespace SmartWorkerAutomation.Common.Automation;

public class UpdateUserRequest
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public int UserTypeId { get; set; }
    public string[]? AllowedCategories { get; set; }
    public string? UpdatedBy { get; set; }
}
