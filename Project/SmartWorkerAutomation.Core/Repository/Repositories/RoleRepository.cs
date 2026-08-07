using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Role> _repository;

    public RoleRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Role>(dbContext);
    }

    public async Task<Role> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Role>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<Role, bool>>)null);
    }

    public void Insert(Role entity) => _repository.Insert(entity);
    public void Update(Role entity) => _repository.Update(entity);

    ~RoleRepository() { _repository = null; }
}
