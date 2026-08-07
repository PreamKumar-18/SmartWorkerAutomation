using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Common.Common;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SalesHeaderRepository : ISalesHeaderRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SalesHeader> _repository;

    public SalesHeaderRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SalesHeader>(dbContext);
    }

    public async Task<SalesHeader> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<SalesHeader>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(SalesHeader entity)
    {
        _repository.Insert(entity);
    }

    public void Update(SalesHeader entity)
    {
        _repository.Update(entity);
    }

    public void Delete(SalesHeader entity)
    {
        _repository.Delete(entity);
    }

    public async Task<SalesHeader> GetSalesHeaderWithItemsAsync(int saleId)
    {
        return await _repository.SearchTop1Async(x => x.SaleId == saleId, includeProperties: "SalesItems");
    }

    public async Task<SalesHeader> GetSalesHeaderFullGraphAsync(int saleId)
    {
        return await _repository.SearchTop1Async(x => x.SaleId == saleId, includeProperties: "SalesItems,SalesPayments");
    }

    public async Task<string> GetLatestBillNoWithPrefixAsync(string prefix)
    {
        var latestSale = await _dbContext.SalesHeaders
            .Where(x => x.BillNo != null && x.BillNo.StartsWith(prefix))
            .OrderByDescending(x => x.BillNo)
            .FirstOrDefaultAsync();
            
        return latestSale?.BillNo;
    }

    public async Task<List<SalesHeader>> GetPaginatedSalesAsync(Paging pagingConfig, Expression<Func<SalesHeader, bool>> filter)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filter, includeProperties: "SalesItems,SalesPayments");
    }

    public async Task<List<SalesHeader>> GetSalesAsync(Expression<Func<SalesHeader, bool>> filter)
    {
        return await _repository.SearchAsync(filter, includeProperties: "SalesItems,SalesPayments");
    }
}
