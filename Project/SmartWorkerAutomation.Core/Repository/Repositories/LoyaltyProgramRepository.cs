using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class LoyaltyProgramRepository : ILoyaltyProgramRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<LoyaltyProgram> _repository;

    public LoyaltyProgramRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<LoyaltyProgram>(dbContext);
    }

    public async Task<LoyaltyProgram> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<LoyaltyProgram>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<LoyaltyProgram, bool>>)null);
    }

    public void Insert(LoyaltyProgram entity) => _repository.Insert(entity);
    public void Update(LoyaltyProgram entity) => _repository.Update(entity);

    public async Task<LoyaltyProgram> GetActiveProgramAsync()
    {
        return await _dbContext.LoyaltyPrograms.FirstOrDefaultAsync(l => l.Status == "Active");
    }

    ~LoyaltyProgramRepository() { _repository = null; }
}
