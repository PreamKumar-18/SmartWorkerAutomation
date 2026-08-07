using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ILocationRepository
{
    Task<Location> GetByIdAsync(int id);
    void Insert(Location entity);
    void Update(Location entity);
    Task<List<Location>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Location, bool>> filter = null);
}
