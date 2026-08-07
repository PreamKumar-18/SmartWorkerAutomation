using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SubCategoryRepository : ISubCategoryRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SubCategory> _repository;

    public SubCategoryRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SubCategory>(dbContext);
    }

    public async Task<SubCategory> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);
    
    public async Task<IEnumerable<SubCategory>> GetPaginatedAsync(Paging pagingConfig, List<Expression<Func<SubCategory, bool>>> filters = null)
        => await _repository.GetPageinatedDataAsync(pagingConfig, filters: filters);    

    public void Insert(SubCategory entity) => _repository.Insert(entity);

    public void Update(SubCategory entity) => _repository.Update(entity);

    ~SubCategoryRepository() { _repository = null; _dbContext.Dispose(); }
}
