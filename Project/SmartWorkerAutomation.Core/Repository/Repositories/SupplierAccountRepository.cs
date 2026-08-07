using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SupplierAccountRepository : ISupplierAccountRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SupplierAccount> _repository;

    public SupplierAccountRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SupplierAccount>(dbContext);
    }

    public async Task<SupplierAccount> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<SupplierAccount>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(SupplierAccount entity)
    {
        _repository.Insert(entity);
    }

    public void Update(SupplierAccount entity)
    {
        _repository.Update(entity);
    }

    public void Delete(SupplierAccount entity)
    {
        _repository.Delete(entity);
    }
}
