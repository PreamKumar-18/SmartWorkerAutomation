using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISupplierRepository
{
    Task<Supplier> GetSupplierByIdAsync(int supplierId);
    Task<IEnumerable<Supplier>> GetPaginatedSuppliersAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Supplier supplier);
    void Update(Supplier supplier);
    Task<int> GetActiveSupplierCountAsync();
    Task<int> GetAllSupplierCountAsync();
    Task<Supplier> GetSupplierFullDetailsAsync(int supplierId);
}
