using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class PurchasePaymentRepository : IPurchasePaymentRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchasePayment> _repository;

    public PurchasePaymentRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchasePayment>(dbContext);
    }

    public async Task<PurchasePayment> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<PurchasePayment>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(PurchasePayment entity)
    {
        _repository.Insert(entity);
    }

    public void Update(PurchasePayment entity)
    {
        _repository.Update(entity);
    }

    public void Delete(PurchasePayment entity)
    {
        _repository.Delete(entity);
    }

    public async Task<decimal> GetTotalPaidAmount(DateTime? fromDate, DateTime? toDate)
    {
        IQueryable<PurchasePayment> query = _dbContext.PurchasePayments;

        if (fromDate.HasValue)
            query = query.Where(x => x.Purchase.PurchaseDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.Purchase.PurchaseDate <= toDate.Value);

        return await query.SumAsync(x => (decimal?)x.PaidAmount) ?? 0;
    }
}
