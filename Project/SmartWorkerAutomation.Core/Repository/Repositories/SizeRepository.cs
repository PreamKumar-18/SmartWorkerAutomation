using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SizeRepository : ISizeRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Size> _repository;

    public SizeRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Size>(dbContext);
    }

    public async Task<Size> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Size>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<Size, bool>>)null);
    }

    public void Insert(Size entity) => _repository.Insert(entity);
    public void Update(Size entity) => _repository.Update(entity);

    ~SizeRepository() { _repository = null; }
}
