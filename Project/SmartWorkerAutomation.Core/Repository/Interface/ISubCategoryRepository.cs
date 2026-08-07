using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISubCategoryRepository
{
    Task<SubCategory> GetByIdAsync(int id);
    Task<IEnumerable<SubCategory>> GetPaginatedAsync(Paging pagingConfig, List<Expression<Func<SubCategory, bool>>> filters = null);
    void Insert(SubCategory entity);
    void Update(SubCategory entity);
}
