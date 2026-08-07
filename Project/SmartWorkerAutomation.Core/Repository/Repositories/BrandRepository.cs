using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Threading.Tasks;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using SmartWorkerAutomation.Common.Common;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class BrandRepository : IBrandRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Brand> _repository;

    public BrandRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Brand>(dbContext);
    }

    public async Task<Brand> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public void Insert(Brand entity)
    {
        _repository.Insert(entity);
    }

    public void Update(Brand entity)
    {
        _repository.Update(entity);
    }

    public async Task<List<Brand>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Brand, bool>> filter = null)
    {
        var data = await _repository.GetPageinatedDataAsync(pagingConfig, filters: filter);
        return data.ToList();
    }
}
