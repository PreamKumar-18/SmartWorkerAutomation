using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchaseItemRepository
{
    Task<PurchaseItem> GetByIdAsync(int id);
    Task<IEnumerable<PurchaseItem>> GetAllAsync();
    void Insert(PurchaseItem entity);
    void Update(PurchaseItem entity);
    void Delete(PurchaseItem entity);
    Task<PurchaseItem> GetPurchaseItemByIdAsync(int PurchaseItemId);
    Task<PurchaseItem> GetPurchaseItemsAsync(int PurchaseItemId, string SupplierName);
}
