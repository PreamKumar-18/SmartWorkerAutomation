using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.RoleDTO;
using SmartWorkerAutomation.Common.Enum;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using SmartWorkerAutomation.Core.Repository.Repositories;
using SmartWorkerAutomation.DataProvider.Interface;
using SmartWorkerAutomation.Transformation.OrganizationTransformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Service;

public class RoleServices : IRoleServices
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IRoleRepository _repo;

    public RoleServices(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repo = new RoleRepository(dbContext);
    }

    public async Task<GenericResponse<RoleResponseDto>> CreateAsync(RoleCreateDto dto)
    {
        var entity = RoleTransformation.ToEntity(dto);
        _repo.Insert(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<RoleResponseDto>(RSCodeEnum.Success, RoleTransformation.ToDto(entity), "Role created.");
    }

    public async Task<GenericResponse<RoleResponseDto>> UpdateAsync(RoleUpdateDto dto)
    {
        var entity = await _repo.GetByIdAsync(dto.RoleId);
        if (entity == null) return new GenericResponse<RoleResponseDto>(RSCodeEnum.NoRecordFound, null, "Not found.");
        RoleTransformation.UpdateEntity(entity, dto);
        _repo.Update(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<RoleResponseDto>(RSCodeEnum.Success, RoleTransformation.ToDto(entity), "Updated.");
    }

    public async Task<GenericPaginatedRes<List<RoleResponseDto>>> GetDetailsAsync(int pageIndex, int pageSize, string sortBy, bool sortAsc)
    {
        var pagingConfig = new Paging { PageIndex = pageIndex, PageSize = pageSize, SortBy = sortBy, SortAsc = sortAsc };
        var paged = await _repo.GetPaginatedAsync(pagingConfig);
        if (paged == null || !paged.Any()) return new GenericPaginatedRes<List<RoleResponseDto>>(RSCodeEnum.NoRecordFound, null, pagingConfig);
        return new GenericPaginatedRes<List<RoleResponseDto>>(RSCodeEnum.Success, paged.Select(RoleTransformation.ToDto).ToList(), pagingConfig);
    }

    public async Task<GenericResponse<RoleResponseDto>> GetByIdAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return new GenericResponse<RoleResponseDto>(RSCodeEnum.NoRecordFound, null, "Not found.");
        return new GenericResponse<RoleResponseDto>(RSCodeEnum.Success, RoleTransformation.ToDto(entity));
    }

    public async Task<GenericResponse<bool>> SoftDeleteAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return new GenericResponse<bool>(RSCodeEnum.NoRecordFound, false, "Not found.");

        var userRepo = new GenericRepository<User>(_dbContext);
        if (await userRepo.AnyAsync(u => u.RoleId == id && u.IsActive))
            throw new Exception("Cannot delete Role because active Users presently inhabit it.");

        entity.IsActive = false;
        entity.UpdatedOn = DateTime.Now;
        _repo.Update(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<bool>(RSCodeEnum.Success, true, "Role deactivated.");
    }
}
