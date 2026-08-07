using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SalesReturnRepository : ISalesReturnRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SalesReturn> _repository;

    public SalesReturnRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SalesReturn>(dbContext);
    }

    public async Task<SalesReturn> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<SalesReturn>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(SalesReturn entity)
    {
        _repository.Insert(entity);
    }

    public void Update(SalesReturn entity)
    {
        _repository.Update(entity);
    }

    public void Delete(SalesReturn entity)
    {
        _repository.Delete(entity);
    }

    public async Task<SalesReturn> GetSalesReturnWithItemsAsync(int saleId)
    {
        return await _repository.SearchTop1Async(x => x.SaleId == saleId, includeProperties: "SalesItems");
    }

    public async Task<SalesReturn> GetSalesReturnFullGraphAsync(int saleId)
    {
        return await _repository.SearchTop1Async(x => x.SaleId == saleId, includeProperties: "SalesItems,SalesPayments");
    }



    public async Task<List<SalesReturn>> GetPaginatedSalesReturnAsync(Paging pagingConfig, Expression<Func<SalesReturn, bool>> filter)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filter, includeProperties: "SalesItems,SalesPayments");
    }

    public async Task<List<SalesReturn>> GetSalesReturnAsync(Expression<Func<SalesReturn, bool>> filter)
    {
        return await _repository.SearchAsync(filter, includeProperties: "SalesReturnItems");
    }
}
