using System;

namespace SmartWorkerAutomation.Common.DTOs.RoleDTO;

public class RoleCreateDto
{
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
}

public class RoleUpdateDto : RoleCreateDto
{
    public int RoleId { get; set; }
}

public class RoleResponseDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
}
