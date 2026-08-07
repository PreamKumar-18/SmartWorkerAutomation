using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchaseReturn> _repository;

    public PurchaseReturnRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchaseReturn>(dbContext);
    }

    public async Task<PurchaseReturn> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<PurchaseReturn>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(PurchaseReturn entity)
    {
        _repository.Insert(entity);
    }

    public void Update(PurchaseReturn entity)
    {
        _repository.Update(entity);
    }

    public void Delete(PurchaseReturn entity)
    {
        _repository.Delete(entity);
    }
}
