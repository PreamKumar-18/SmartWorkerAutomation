using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IItemSupplierRepository
{
    Task<IEnumerable<ItemSupplier>> GetItemSuppliersBySupplierIdAsync(int supplierId);
    void Insert(ItemSupplier itemSupplier);
    void Update(ItemSupplier itemSupplier);
}
