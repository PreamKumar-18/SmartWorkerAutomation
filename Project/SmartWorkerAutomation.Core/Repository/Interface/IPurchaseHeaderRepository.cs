using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchaseHeaderRepository
{
    Task<PurchaseHeader> GetByIdAsync(int id);
    void Insert(PurchaseHeader entity);
    void Update(PurchaseHeader entity);
    Task<IEnumerable<PurchaseHeader>> GetPurchaseHistoryBySupplierIdAsync(int supplierId);
    void Delete(PurchaseHeader entity);
    Task<PurchaseHeader> GetPurchaseHeaderWithItemsAsync(int purchaseId);
    Task<System.Collections.Generic.List<PurchaseHeader>> GetPaginatedPurchaseHeadersAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig, System.DateTime? fromDate, System.DateTime? toDate);
    System.Linq.IQueryable<PurchaseHeader> GetAllQueryable();

    Task<decimal> GetPurchaseInvoiceAmount(DateTime? fromDate, DateTime? toDate);
}
