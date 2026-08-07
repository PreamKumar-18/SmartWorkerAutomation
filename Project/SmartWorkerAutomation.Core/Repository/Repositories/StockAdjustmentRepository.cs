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

public class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<StockAdjustment> _repository;
    

    public StockAdjustmentRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<StockAdjustment>(dbContext);
       
    }

    public void Insert(StockAdjustment stockAdjustment)
    {
        _repository.Insert(stockAdjustment);
    }

    public async Task<List<StockAdjustment>> GetPaginatedStockAdjustmentsAsync(
    Paging pagingConfig,
    int itemId)
    {
        return await _repository.GetPageinatedDataAsync(
            pagingConfig,
            filters: sa => sa.StockAdjustmentItems.Any(i => i.ItemId == itemId),
            includeProperties: "StockAdjustmentItems"
        );
    }
}
