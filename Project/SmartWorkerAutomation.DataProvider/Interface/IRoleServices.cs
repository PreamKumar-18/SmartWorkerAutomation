using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.RoleDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Interface;

public interface IRoleServices
{
    Task<GenericResponse<RoleResponseDto>> CreateAsync(RoleCreateDto dto);
    Task<GenericResponse<RoleResponseDto>> UpdateAsync(RoleUpdateDto dto);
    Task<GenericPaginatedRes<List<RoleResponseDto>>> GetDetailsAsync(int pageIndex, int pageSize, string sortBy, bool sortAsc);
    Task<GenericResponse<RoleResponseDto>> GetByIdAsync(int id);
    Task<GenericResponse<bool>> SoftDeleteAsync(int id);
}
