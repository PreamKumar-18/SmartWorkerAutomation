using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class BranchRepository : IBranchRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Branch> _repository;

    public BranchRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Branch>(dbContext);
    }

    public async Task<Branch> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Branch>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<Branch, bool>>)null);
    }

    public void Insert(Branch entity) => _repository.Insert(entity);
    public void Update(Branch entity) => _repository.Update(entity);

    ~BranchRepository() { _repository = null; }
}
