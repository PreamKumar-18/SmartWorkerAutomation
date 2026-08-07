using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IColourRepository
{
    Task<Colour> GetByIdAsync(int id);
    Task<IEnumerable<Colour>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Colour entity);
    void Update(Colour entity);
}
