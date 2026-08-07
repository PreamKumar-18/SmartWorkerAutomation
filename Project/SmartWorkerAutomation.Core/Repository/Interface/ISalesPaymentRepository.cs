using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISalesPaymentRepository
{
    Task<SalesPayment> GetByIdAsync(int id);
    Task<IEnumerable<SalesPayment>> GetAllAsync();
    void Insert(SalesPayment entity);
    void Update(SalesPayment entity);
    void Delete(SalesPayment entity);
    Task<List<SalesPayment>> GetSalesPaymentsAsync(Expression<Func<SalesPayment, bool>> filter);
}
