using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Common;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISalesHeaderRepository
{
    Task<SalesHeader> GetByIdAsync(int id);
    Task<IEnumerable<SalesHeader>> GetAllAsync();
    void Insert(SalesHeader entity);
    void Update(SalesHeader entity);
    void Delete(SalesHeader entity);
    Task<SalesHeader> GetSalesHeaderWithItemsAsync(int saleId);
    Task<SalesHeader> GetSalesHeaderFullGraphAsync(int saleId);
    Task<string> GetLatestBillNoWithPrefixAsync(string prefix);
    Task<List<SalesHeader>> GetPaginatedSalesAsync(Paging pagingConfig, Expression<System.Func<SalesHeader, bool>> filter);
    Task<List<SalesHeader>> GetSalesAsync(Expression<Func<SalesHeader, bool>> filter);
}
