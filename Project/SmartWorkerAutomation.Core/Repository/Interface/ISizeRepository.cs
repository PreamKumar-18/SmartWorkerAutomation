using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISizeRepository
{
    Task<Size> GetByIdAsync(int id);
    Task<IEnumerable<Size>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Size entity);
    void Update(Size entity);
}
