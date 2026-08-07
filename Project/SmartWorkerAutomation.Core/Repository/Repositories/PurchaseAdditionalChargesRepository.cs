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

public class PurchaseAdditionalChargesRepository : IPurchaseAdditionalChargesRepository
{

    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<PurchaseAdditionalCharge> _repository;

    public PurchaseAdditionalChargesRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<PurchaseAdditionalCharge>(dbContext);
    }

    public async Task<PurchaseAdditionalCharge> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<PurchaseAdditionalCharge>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public void Insert(PurchaseAdditionalCharge entity)
    {
        _repository.Insert(entity);
    }

    public void Update(PurchaseAdditionalCharge entity)
    {
        _repository.Update(entity);
    }

    public void Delete(PurchaseAdditionalCharge entity)
    {
        _repository.Delete(entity);
    }
}
