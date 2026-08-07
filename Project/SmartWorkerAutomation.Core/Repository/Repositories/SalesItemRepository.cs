using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SalesItemRepository : ISalesItemRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SalesItem> _repository;

    public SalesItemRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SalesItem>(dbContext);
    }

    public async Task<SalesItem> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<SalesItem>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(SalesItem entity)
    {
        _repository.Insert(entity);
    }

    public void Update(SalesItem entity)
    {
        _repository.Update(entity);
    }

    public void Delete(SalesItem entity)
    {
        _repository.Delete(entity);
    }
}
