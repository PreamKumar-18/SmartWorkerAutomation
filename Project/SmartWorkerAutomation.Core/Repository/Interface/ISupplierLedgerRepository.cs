using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISupplierLedgerRepository
{
    Task<IEnumerable<SupplierLedger>> GetLedgerBySupplierIdAsync(int supplierId);
    void Insert(SupplierLedger supplierLedger);
    Task<SupplierLedger> GetLastLedgerBySupplierIdAsync(int supplierId);
}
