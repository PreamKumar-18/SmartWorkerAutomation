using System;

namespace SmartWorkerAutomation.Common.DTOs.UserDTO;

public class UserCreateDto
{
    public int? BranchId { get; set; }
    public int? RoleId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // Mapped to Hash
    public string PinCode { get; set; } = string.Empty;
    public string DefaultScreen { get; set; } = string.Empty;
}

public class UserUpdateDto
{
    public int UserId { get; set; }
    public int? BranchId { get; set; }
    public int? RoleId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string DefaultScreen { get; set; } = string.Empty;
}

public class UserResponseDto
{
    public int UserId { get; set; }
    public int? BranchId { get; set; }
    public int? RoleId { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public string DefaultScreen { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedOn { get; set; }
}
