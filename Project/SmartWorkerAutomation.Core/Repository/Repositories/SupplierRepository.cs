using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Supplier> _repository;

    public SupplierRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Supplier>(dbContext);
    }

    public async Task<Supplier> GetSupplierByIdAsync(int supplierId)
    {
        return await _repository.GetByIdAsync(supplierId);
    }

    public async Task<IEnumerable<Supplier>> GetPaginatedSuppliersAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<Func<Supplier, bool>>)null, includeProperties: "SupplierAccounts,PurchaseHeaders,PurchaseHeaders.PurchasePayments");
    }

    public void Insert(Supplier supplier)
    {
        _repository.Insert(supplier);
    }

    public void Update(Supplier supplier)
    {
        _repository.Update(supplier);
    }

    public async Task<int> GetActiveSupplierCountAsync()
    {
        return await _repository.SearchCountAsync(x => x.IsActive == true);
    }

    public async Task<int> GetAllSupplierCountAsync()
    {
        return await _repository.SearchCountAsync();
    }
    public async Task<Supplier> GetSupplierFullDetailsAsync(int supplierId)
    {
        return await _repository.SearchTop1Async(
            x => x.SupplierId == supplierId,
           includeProperties: "SupplierAccounts,SupplierLedgers,PurchaseHeaders.PurchasePayments"

        );
    }
    ~SupplierRepository()
    {
        _dbContext.Dispose();
        _repository = null;
    }

}
