using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IBranchRepository
{
    Task<Branch> GetByIdAsync(int id);
    Task<IEnumerable<Branch>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Branch entity);
    void Update(Branch entity);
}
