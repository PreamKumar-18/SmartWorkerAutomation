using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchasePaymentRepository
{
    Task<PurchasePayment> GetByIdAsync(int id);
    Task<IEnumerable<PurchasePayment>> GetAllAsync();
    void Insert(PurchasePayment entity);
    void Update(PurchasePayment entity);
    void Delete(PurchasePayment entity);
    Task<decimal> GetTotalPaidAmount(DateTime? fromDate, DateTime? toDate);
}
