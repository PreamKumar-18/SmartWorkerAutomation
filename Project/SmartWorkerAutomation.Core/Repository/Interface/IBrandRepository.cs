using SmartWorkerAutomation.Core.Models;
using System.Threading.Tasks;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using SmartWorkerAutomation.Common.Common;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IBrandRepository
{
    Task<Brand> GetByIdAsync(int id);
    void Insert(Brand entity);
    void Update(Brand entity);
    Task<List<Brand>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Brand, bool>> filter = null);
}
