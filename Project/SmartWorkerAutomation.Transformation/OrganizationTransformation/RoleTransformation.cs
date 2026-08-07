using SmartWorkerAutomation.Common.DTOs.RoleDTO;
using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Transformation.OrganizationTransformation;

public static class RoleTransformation
{
    public static Role ToEntity(RoleCreateDto dto)
    {
        return new Role
        {
            RoleName = dto.RoleName,
            Description = dto.Description,
            IsSystemRole = dto.IsSystemRole,
            IsActive = true,
            CreatedOn = System.DateTime.Now,
            UpdatedOn = System.DateTime.Now
        };
    }

    public static RoleResponseDto ToDto(Role entity)
    {
        return new RoleResponseDto
        {
            RoleId = entity.RoleId,
            RoleName = entity.RoleName,
            Description = entity.Description,
            IsSystemRole = entity.IsSystemRole,
            IsActive = entity.IsActive,
            CreatedOn = entity.CreatedOn
        };
    }

    public static void UpdateEntity(Role entity, RoleUpdateDto dto)
    {
        entity.RoleName = dto.RoleName;
        entity.Description = dto.Description;
        entity.IsSystemRole = dto.IsSystemRole;
        entity.UpdatedOn = System.DateTime.Now;
    }
}
