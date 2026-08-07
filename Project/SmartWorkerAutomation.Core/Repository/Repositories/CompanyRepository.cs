using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Company> _repository;

    public CompanyRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Company>(dbContext);
    }

    public async Task<Company> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Company>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<Company, bool>>)null);
    }

    public void Insert(Company entity) => _repository.Insert(entity);
    public void Update(Company entity) => _repository.Update(entity);

    ~CompanyRepository() { _repository = null; }
}
