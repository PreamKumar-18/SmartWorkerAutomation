using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.CategoryDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Interface;

public interface ICategoryServices
{
    Task<GenericResponse<CategoryResponseDto>> CreateAsync(CategoryCreateDto dto);
    Task<GenericResponse<CategoryResponseDto>> UpdateAsync(CategoryUpdateDto dto);
    Task<GenericPaginatedRes<List<CategoryResponseDto>>> GetDetailsAsync(int pageIndex, int pageSize, string sortBy, bool sortAsc);
    Task<GenericResponse<CategoryResponseDto>> GetByIdAsync(int id);
    Task<GenericResponse<bool>> SoftDeleteAsync(int id);
}
