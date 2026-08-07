using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class TaxMasterRepository : ITaxMasterRepository
{

    private readonly IGenericRepository<TaxMaster> _repository;
    private readonly SmartWorkerAutomationContext _dbContext;

    public TaxMasterRepository(SmartWorkerAutomationContext dbcontext)
    {
        _dbContext = dbcontext;
        _repository = new GenericRepository<TaxMaster>(dbcontext);
    }
    public async Task<TaxMaster> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);


    public async Task<IEnumerable<TaxMaster>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, x => x.IsActive == true);
    }


    public void Insert(TaxMaster entity) => _repository.Insert(entity);



    public void Update(TaxMaster entity) => _repository.Update(entity);

}
