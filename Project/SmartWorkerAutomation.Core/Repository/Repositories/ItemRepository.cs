using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Item> _repository;

    public ItemRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Item>(dbContext);
    }

    public async Task<Item> GetItemByIdAsync(int itemId)
    {
        return await _repository.SearchTop1Async(filters: x => x.ItemId == itemId, includeProperties: "PurchaseTax,SalesTax");
    }

    public async Task<List<Item>> GetItemByBarcodeAsync(string searchTerm, Paging pagingConfig)
    {
        Expression < Func<Item, bool> > expressions = x => x.BarcodeValue.Contains(searchTerm) || x.ItemName.Contains(searchTerm) || x.Category.CategoryName.Contains(searchTerm);
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: expressions, includeProperties: "Category,PurchaseTax,SalesTax,ItemVariants");
    }

    public async Task<string> GenerateNextItemCodeAsync()
    {
        var lastNumber = _repository.GetAllQueryable()
            .Select(x => x.ItemCode).AsEnumerable()
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .DefaultIfEmpty(100).Max();
        return (lastNumber + 1).ToString();
    }

    public async Task<List<Item>> GetPaginatedItemsAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (Expression<Func<Item, bool>>) null, includeProperties: "PurchaseTax,SalesTax,ItemVariants");
    }

    public async Task<List<Item>> GetPaginatedItemsByPriceAsync(Paging pagingConfig, decimal mrp)
    {
        Expression<Func<Item, bool>> filter = x =>
     (x.Mrp == mrp);
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: filter);
    }

    public void Insert(Item item)
    {
        _repository.Insert(item);
    }

    public void Update(Item item)
    {
        _repository.Update(item);
    }
    public IQueryable<Item> GetAllQueryable()
    {
        return _repository.GetAllQueryable();
    }
    ~ItemRepository()
    {
        _dbContext.Dispose();
        _repository = null;
    }

}
