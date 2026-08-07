using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class PurchaseItemRepository : IPurchaseItemRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchaseItem> _repository;

    public PurchaseItemRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchaseItem>(dbContext);
    }

    public async Task<PurchaseItem> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<PurchaseItem>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }
    public async Task<PurchaseItem> GetPurchaseItemByIdAsync(int PurchaseItemId)
    {
        return await _repository.SearchTop1Async(filters: x => x.PurchaseItemId == PurchaseItemId, includeProperties: "Item");
    }
    public void Insert(PurchaseItem entity)
    {
        _repository.Insert(entity);
    }

    public void Update(PurchaseItem entity)
    {
        _repository.Update(entity);
    }

    public void Delete(PurchaseItem entity)
    {
        _repository.Delete(entity);
    }

    public async Task<PurchaseItem> GetPurchaseItemsAsync(int PurchaseItemId, string SupplierName)
    {
        return await _repository.SearchTop1Async(x => x.PurchaseItemId == PurchaseItemId && x.SupplierName == SupplierName);
    }
}
