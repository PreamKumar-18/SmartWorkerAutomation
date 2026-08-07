using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Linq;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class PurchaseActivityLogRepository : IPurchaseActivityLogRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchaseActivityLog> _repository;

    public PurchaseActivityLogRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchaseActivityLog>(dbContext);
    }

    public void Insert(PurchaseActivityLog entity)
    {
        _repository.Insert(entity);
    }

    public IQueryable<PurchaseActivityLog> GetAllQueryable()
    {
        return _repository.GetAllQueryable();
    }
}
