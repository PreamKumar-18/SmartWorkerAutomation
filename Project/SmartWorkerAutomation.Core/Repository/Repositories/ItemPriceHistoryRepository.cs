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

public class ItemPriceHistoryRepository : IItemPriceHistoryRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<ItemPriceHistory> _repository;

    public ItemPriceHistoryRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<ItemPriceHistory>(dbContext);
    }
    public async Task<ItemPriceHistory> GetItemPriceHistoryByIdAsync(int itemId)
    {
        return await _repository.SearchTop1Async(x=>x.ItemId ==itemId);
    }
}
