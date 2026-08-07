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

public class LocationRepository : ILocationRepository
{

    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Location> _repository;

    public LocationRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Location>(dbContext);
    }

    public async Task<Location> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public void Insert(Location entity)
    {
        _repository.Insert(entity);
    }

    public void Update(Location entity)
    {
        _repository.Update(entity);
    }

    public async Task<List<Location>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<Location, bool>> filter = null)
    {
        var data = await _repository.GetPageinatedDataAsync(pagingConfig, filters: filter);
        return data.ToList();
    }
}
