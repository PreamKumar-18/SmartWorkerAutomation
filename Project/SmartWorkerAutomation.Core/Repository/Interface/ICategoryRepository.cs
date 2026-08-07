using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ICategoryRepository
{
    Task<Category> GetByIdAsync(int id);
    Task<IEnumerable<Category>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Category entity);
    void Update(Category entity);
}
