using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class StockLedgerRepository : IStockLedgerRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<StockLedger> _repository;

    public StockLedgerRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<StockLedger>(dbContext);
    }

    public async Task<StockLedger> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<StockLedger>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(StockLedger entity)
    {
        _repository.Insert(entity);
    }

    public void Update(StockLedger entity)
    {
        _repository.Update(entity);
    }

    public void Delete(StockLedger entity)
    {
        _repository.Delete(entity);
    }

    public async Task<List<StockLedger>> GetPaginatedStockLedgerAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<Func<StockLedger, bool>>)null);
    }

    public async Task<StockLedger> GetStockLedgerByIdAsync(int itemId)
    {
        return await _repository.SearchTop1Async(x => x.ItemId == itemId);
    }

    public async Task<List<StockLedger>> GetOutOfStockPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(
            pagingConfig,
            filters: sl => (sl.BalanceQty ?? 0) <= 0,
            includeProperties: "Item"
        );
    }

    public async Task<List<StockLedger>> GetLowStockPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(
            pagingConfig,
            filters: sl => (sl.BalanceQty ?? 0) <= sl.Item.MinStockQty,
            includeProperties: "Item"
        );
    }
    public IQueryable<StockLedger> GetAllQueryable()
    {
        return _repository.GetAllQueryable();
    }
}
