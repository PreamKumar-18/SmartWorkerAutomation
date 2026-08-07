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


public class StockAdjustmentItemRepository : IStockAdjustmentItemRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<StockAdjustmentItem> _repository;

    public StockAdjustmentItemRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<StockAdjustmentItem>(dbContext);
    }

    public void Insert(StockAdjustmentItem stockAdjustmentItem)
    {
        _repository.Insert(stockAdjustmentItem);
    }

    public async Task InsertManyAsync(List<StockAdjustmentItem> items)
    {
        _repository.InsertMany(items);
    }
}
