using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IRoleRepository
{
    Task<Role> GetByIdAsync(int id);
    Task<IEnumerable<Role>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Role entity);
    void Update(Role entity);
}
