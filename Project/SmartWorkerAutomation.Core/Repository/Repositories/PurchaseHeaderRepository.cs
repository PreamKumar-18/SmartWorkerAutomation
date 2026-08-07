using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class PurchaseHeaderRepository : IPurchaseHeaderRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchaseHeader> _repository;

    public PurchaseHeaderRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchaseHeader>(dbContext);
    }

    public async Task<PurchaseHeader> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public void Insert(PurchaseHeader entity)
    {
        _repository.Insert(entity);
    }

    public void Update(PurchaseHeader entity)
    {
        _repository.Update(entity);
    }

    public async Task<IEnumerable<PurchaseHeader>> GetPurchaseHistoryBySupplierIdAsync(int supplierId)
    {
        return await _repository.SearchAsync(p => p.SupplierId == supplierId);
    }

    public async Task<PurchaseHeader> GetPurchaseHeaderWithItemsAsync(int purchaseId)
    {
        return await _repository.SearchTop1Async(x => x.PurchaseId == purchaseId, includeProperties: "PurchaseItems,PurchaseAdditionalCharges,Supplier,PurchasePayments,PurchaseStatus,Location");
    }

    public async Task<System.Collections.Generic.List<PurchaseHeader>> GetPaginatedPurchaseHeadersAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig, System.DateTime? fromDate, System.DateTime? toDate)
    {
        var filters = new System.Collections.Generic.List<System.Linq.Expressions.Expression<System.Func<PurchaseHeader, bool>>>();
        if (fromDate.HasValue) filters.Add(x => x.PurchaseDate >= fromDate.Value);
        if (toDate.HasValue) filters.Add(x => x.PurchaseDate <= toDate.Value);

        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: filters, includeProperties: "PurchaseItems,PurchaseAdditionalCharges,Supplier,PurchasePayments,PurchaseStatus,Location");
    }

    public async Task<decimal> GetPurchaseInvoiceAmount(DateTime? fromDate, DateTime? toDate)
    {
        var filters = new List<Expression<Func<PurchaseHeader, bool>>>();

        if (fromDate.HasValue)
            filters.Add(x => x.PurchaseDate >= fromDate.Value);

        if (toDate.HasValue)
            filters.Add(x => x.PurchaseDate <= toDate.Value);

        var purchaseHeaders = await _repository.SearchAsync(filters);

        return purchaseHeaders.Sum(x => x.InvoiceAmount);
    }


    public void Delete(PurchaseHeader entity)
    {
        _repository.Delete(entity);
    }

    public System.Linq.IQueryable<PurchaseHeader> GetAllQueryable()
    {
        return _repository.GetAllQueryable();
    }
}
