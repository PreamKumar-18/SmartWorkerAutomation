using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ICustomerRepository
{
    Task<Customer> GetByIdAsync(int id);
    Task<IEnumerable<Customer>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig, Expression<Func<Customer, bool>> filters = null);
    void Insert(Customer entity);
    void Update(Customer entity);
}
