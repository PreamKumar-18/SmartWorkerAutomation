using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using Microsoft.EntityFrameworkCore;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class ItemSupplierRepository : IItemSupplierRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<ItemSupplier> _repository;

    public ItemSupplierRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<ItemSupplier>(dbContext);
    }

    public async Task<IEnumerable<ItemSupplier>> GetItemSuppliersBySupplierIdAsync(int supplierId)
    {
        return await _dbContext.ItemSuppliers.Where(i => i.SupplierId == supplierId).ToListAsync();
    }

    public void Insert(ItemSupplier itemSupplier)
    {
        _repository.Insert(itemSupplier);
    }

    public void Update(ItemSupplier itemSupplier)
    {
        _repository.Update(itemSupplier);
    }

}
