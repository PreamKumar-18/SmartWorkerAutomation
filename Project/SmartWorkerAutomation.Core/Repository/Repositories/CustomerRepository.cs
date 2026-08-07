using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Customer> _repository;

    public CustomerRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Customer>(dbContext);
    }

    public async Task<Customer> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);
    
    public async Task<IEnumerable<Customer>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Customer, bool>> filters = null)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: filters);
    }

    public void Insert(Customer entity) => _repository.Insert(entity);
    public void Update(Customer entity) => _repository.Update(entity);

    ~CustomerRepository() { _repository = null; }
}
