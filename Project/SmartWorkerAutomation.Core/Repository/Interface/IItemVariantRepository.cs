using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IItemVariantRepository
{
    Task<ItemVariant> GetByIdAsync(int id);
    Task<IEnumerable<ItemVariant>> GetAllAsync();
    void Insert(ItemVariant entity);
    void Update(ItemVariant entity);
    void Delete(ItemVariant entity);
    Task<List<ItemVariant>> GetVariantsByItemIdAsync(int itemId);
    IQueryable<ItemVariant> GetAllQueryable();
    Task<int> GetTotalVariantsCountAsync();
}
