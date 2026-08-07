using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class PurchaseStatusRepository : IPurchaseStatusRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchaseStatus> _repository;

    public PurchaseStatusRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchaseStatus>(dbContext);
    }

    public async Task<PurchaseStatus> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public void Insert(PurchaseStatus entity)
    {
        _repository.Insert(entity);
    }

    public void Update(PurchaseStatus entity)
    {
        _repository.Update(entity);
    }

    public async Task<List<PurchaseStatus>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<PurchaseStatus, bool>> filter = null)
    {
        var data = await _repository.GetPageinatedDataAsync(pagingConfig, filters: filter);
        return data.ToList();
    }
}
