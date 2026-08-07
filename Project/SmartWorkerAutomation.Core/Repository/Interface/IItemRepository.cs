using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IItemRepository
{
    Task<Item> GetItemByIdAsync(int itemId);
    Task<List<Item>> GetPaginatedItemsAsync(Paging pagingConfig);
    Task<List<Item>> GetItemByBarcodeAsync(string barcode, Paging pagingConfig);
    void Insert(Item item);
    void Update(Item item);
    Task<List<Item>> GetPaginatedItemsByPriceAsync(Paging pagingConfig, decimal mrp);
    Task<string> GenerateNextItemCodeAsync();
    IQueryable<Item> GetAllQueryable();


}
