using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.CategoryDTO;
using SmartWorkerAutomation.Common.Enum;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using SmartWorkerAutomation.Core.Repository.Repositories;
using SmartWorkerAutomation.DataProvider.Interface;
using SmartWorkerAutomation.Transformation.MasterTransformation;

namespace SmartWorkerAutomation.DataProvider.Service;

public class CategoryServices : ICategoryServices
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private ICategoryRepository _repo;

    public CategoryServices(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repo = new CategoryRepository(dbContext);
    }

    public async Task<GenericResponse<CategoryResponseDto>> CreateAsync(CategoryCreateDto dto)
    {
        var entity = CategoryTransformation.ToEntity(dto);
        _repo.Insert(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<CategoryResponseDto>(RSCodeEnum.Success, CategoryTransformation.ToDto(entity), "Category created.");
    }

    public async Task<GenericResponse<CategoryResponseDto>> UpdateAsync(CategoryUpdateDto dto)
    {
        var entity = await _repo.GetByIdAsync(dto.CategoryId);
        if (entity == null) return new GenericResponse<CategoryResponseDto>(RSCodeEnum.NoRecordFound, null, "Not found.");
        CategoryTransformation.UpdateEntity(entity, dto);
        _repo.Update(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<CategoryResponseDto>(RSCodeEnum.Success, CategoryTransformation.ToDto(entity), "Updated.");
    }

    public async Task<GenericPaginatedRes<List<CategoryResponseDto>>> GetDetailsAsync(int pageIndex, int pageSize, string sortBy, bool sortAsc)
    {
        var pagingConfig = new Paging { PageIndex = pageIndex, PageSize = pageSize, SortBy = sortBy, SortAsc = sortAsc };
        var paged = await _repo.GetPaginatedAsync(pagingConfig);
        if (paged == null || !paged.Any()) return new GenericPaginatedRes<List<CategoryResponseDto>>(RSCodeEnum.NoRecordFound, null, pagingConfig);
        return new GenericPaginatedRes<List<CategoryResponseDto>>(RSCodeEnum.Success, paged.Select(CategoryTransformation.ToDto).ToList(), pagingConfig);
    }

    public async Task<GenericResponse<CategoryResponseDto>> GetByIdAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return new GenericResponse<CategoryResponseDto>(RSCodeEnum.NoRecordFound, null, "Not found.");
        return new GenericResponse<CategoryResponseDto>(RSCodeEnum.Success, CategoryTransformation.ToDto(entity));
    }

    public async Task<GenericResponse<bool>> SoftDeleteAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return new GenericResponse<bool>(RSCodeEnum.NoRecordFound, false, "Not found.");

        var subCatRepo = new GenericRepository<SubCategory>(_dbContext);
        if (await subCatRepo.AnyAsync(s => s.CategoryId == id && s.IsActive))
            throw new Exception("Cannot delete Category because active SubCategories exist.");

        var itemRepo = new GenericRepository<Item>(_dbContext);
        if (await itemRepo.AnyAsync(i => i.CategoryId == id && i.IsActive))
            throw new Exception("Cannot delete Category because active Items exist.");

        entity.IsActive = false;
        entity.UpdatedOn = DateTime.Now;
        _repo.Update(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<bool>(RSCodeEnum.Success, true, "Category deactivated.");
    }
}
