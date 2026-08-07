using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using Microsoft.EntityFrameworkCore;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SupplierLedgerRepository : ISupplierLedgerRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SupplierLedger> _repository;

    public SupplierLedgerRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SupplierLedger>(dbContext);
    }

    public async Task<IEnumerable<SupplierLedger>> GetLedgerBySupplierIdAsync(int supplierId)
    {
        return await _repository.SearchAsync(sl => sl.SupplierId == supplierId);
    }

    public void Insert(SupplierLedger supplierLedger)
    {
        _repository.Insert(supplierLedger);
    }

    public void Delete(SupplierLedger entity)
    {
        _repository.Delete(entity);
    }

    public async Task<SupplierLedger> GetLastLedgerBySupplierIdAsync(int supplierId)
    {
        return await _repository.SearchTop1Async(
            filters: x => x.SupplierId == supplierId, 
            orderBy: q => q.OrderByDescending(o => o.CreatedOn)
        );
    }
}
