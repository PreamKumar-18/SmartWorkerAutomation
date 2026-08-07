using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class SalesPaymentRepository : ISalesPaymentRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<SalesPayment> _repository;

    public SalesPaymentRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<SalesPayment>(dbContext);
    }

    public async Task<SalesPayment> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<SalesPayment>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(SalesPayment entity)
    {
        _repository.Insert(entity);
    }

    public void Update(SalesPayment entity)
    {
        _repository.Update(entity);
    }

    public void Delete(SalesPayment entity)
    {
        _repository.Delete(entity);
    }

    public async Task<List<SalesPayment>> GetSalesPaymentsAsync(Expression<Func<SalesPayment, bool>> filter)
    {
        return await _repository.SearchAsync(filter, includeProperties: "PaymentMode");
    }
}
