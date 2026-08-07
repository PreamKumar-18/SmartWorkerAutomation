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

public class StockTransferItemRepository : IStockTransferItemRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<StockTransferItem> _repository;

    public StockTransferItemRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<StockTransferItem>(dbContext);
    }

    public void Insert(StockTransferItem stockAdjustmentItem)
    {
        _repository.Insert(stockAdjustmentItem);
    }

    public async Task InsertManyAsync(List<StockTransferItem> items)
    {
        _repository.InsertMany(items);
    }
}
