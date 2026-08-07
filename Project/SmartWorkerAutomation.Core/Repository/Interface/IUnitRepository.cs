using SmartWorkerAutomation.Core.Models;
using System.Threading.Tasks;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using SmartWorkerAutomation.Common.Common;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IUnitRepository
{
    Task<Unit> GetByIdAsync(int id);
    void Insert(Unit entity);
    void Update(Unit entity);
    Task<List<Unit>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Unit, bool>> filter = null);
}
