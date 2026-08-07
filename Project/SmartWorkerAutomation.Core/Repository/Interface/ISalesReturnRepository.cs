using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISalesReturnRepository
{
    Task<SalesReturn> GetByIdAsync(int id);
    Task<IEnumerable<SalesReturn>> GetAllAsync();
    void Insert(SalesReturn entity);
    void Update(SalesReturn entity);
    void Delete(SalesReturn entity);
    Task<SalesReturn> GetSalesReturnWithItemsAsync(int saleId);
    Task<SalesReturn> GetSalesReturnFullGraphAsync(int saleId);
    Task<List<SalesReturn>> GetPaginatedSalesReturnAsync(Paging pagingConfig, Expression<System.Func<SalesReturn, bool>> filter);
    Task<List<SalesReturn>> GetSalesReturnAsync(Expression<Func<SalesReturn, bool>> filter);
}

