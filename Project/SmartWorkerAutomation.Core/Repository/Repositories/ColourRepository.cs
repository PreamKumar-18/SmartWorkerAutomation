using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class ColourRepository : IColourRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Colour> _repository;

    public ColourRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Colour>(dbContext);
    }

    public async Task<Colour> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Colour>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: x => x.IsActive);
    }

    public void Insert(Colour entity) => _repository.Insert(entity);
    public void Update(Colour entity) => _repository.Update(entity);

    ~ColourRepository() { _repository = null; }
}
