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

public class UnitRepository : IUnitRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Unit> _repository;

    public UnitRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Unit>(dbContext);
    }

    public async Task<Unit> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public void Insert(Unit entity)
    {
        _repository.Insert(entity);
    }

    public void Update(Unit entity)
    {
        _repository.Update(entity);
    }

    public async Task<List<Unit>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Unit, bool>> filter = null)
    {
        var data = await _repository.GetPageinatedDataAsync(pagingConfig, filters: filter);
        return data.ToList();
    }
}
