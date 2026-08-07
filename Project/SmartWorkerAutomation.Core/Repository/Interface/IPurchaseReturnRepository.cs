using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchaseReturnRepository
{
    Task<PurchaseReturn> GetByIdAsync(int id);
    Task<IEnumerable<PurchaseReturn>> GetAllAsync();
    void Insert(PurchaseReturn entity);
    void Update(PurchaseReturn entity);
    void Delete(PurchaseReturn entity);
}
