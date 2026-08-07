using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class ItemVariantRepository : IItemVariantRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<ItemVariant> _repository;

    public ItemVariantRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<ItemVariant>(dbContext);
    }

    public async Task<ItemVariant> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<ItemVariant>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(ItemVariant entity)
    {
        _repository.Insert(entity);
    }

    public void Update(ItemVariant entity)
    {
        _repository.Update(entity);
    }

    public void Delete(ItemVariant entity)
    {
        _repository.Delete(entity);
    }

    public async Task<List<ItemVariant>> GetVariantsByItemIdAsync(int itemId)
    {
        return await _repository.SearchAsync(v => v.ItemId == itemId);
    }
    public IQueryable<ItemVariant> GetAllQueryable()
    {
        return _repository.GetAllQueryable();
    }

    public async Task<int> GetTotalVariantsCountAsync()
    {
        return await _repository.SearchCountAsync();
    }
}
