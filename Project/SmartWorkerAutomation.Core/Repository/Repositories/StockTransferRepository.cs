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

public class StockTransferRepository : IStockTransferRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<StockTransfer> _repository;
   

    public StockTransferRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<StockTransfer>(dbContext);
        
    }

    public void Insert(StockTransfer stockTransfer)
    {
        _repository.Insert(stockTransfer);
    }

    public async Task<List<StockTransfer>> GetPaginatedStockTransferAsync(
    Paging pagingConfig,
    int itemId)
    {
        return await _repository.GetPageinatedDataAsync(
            pagingConfig,
            filters: sa => sa.StockTransferItems.Any(i => i.ItemId == itemId),
            includeProperties: "StockTransferItems"
        );
    }
}
