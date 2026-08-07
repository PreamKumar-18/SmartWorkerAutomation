using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISalesItemRepository
{
    Task<SalesItem> GetByIdAsync(int id);
    Task<IEnumerable<SalesItem>> GetAllAsync();
    void Insert(SalesItem entity);
    void Update(SalesItem entity);
    void Delete(SalesItem entity);
}
