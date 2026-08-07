using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Category> _repository;

    public CategoryRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Category>(dbContext);
    }

    public async Task<Category> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Category>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<Category, bool>>)null);
    }

    public void Insert(Category entity) => _repository.Insert(entity);
    public void Update(Category entity) => _repository.Update(entity);

    ~CategoryRepository() { _repository = null; }
}
