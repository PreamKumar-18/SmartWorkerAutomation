using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IStockLedgerRepository
{
    Task<List<StockLedger>> GetPaginatedStockLedgerAsync(Paging pagingConfig);
    Task<StockLedger> GetStockLedgerByIdAsync(int itemId);
    Task<StockLedger> GetByIdAsync(int id);
    Task<IEnumerable<StockLedger>> GetAllAsync();
    void Insert(StockLedger entity);
    void Update(StockLedger entity);
    void Delete(StockLedger entity);

    Task<List<StockLedger>> GetOutOfStockPaginatedAsync(Paging pagingConfig);
    Task<List<StockLedger>> GetLowStockPaginatedAsync(Paging pagingConfig);
    IQueryable<StockLedger> GetAllQueryable();

}
