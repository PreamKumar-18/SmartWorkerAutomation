using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class OfferRepository : IOfferRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Offer> _repository;

    public OfferRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Offer>(dbContext);
    }

    public async Task<Offer> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Offer>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<Func<Offer, bool>>)null);
    }

    public void Insert(Offer entity) => _repository.Insert(entity);
    public void Update(Offer entity) => _repository.Update(entity);

    public async Task<Offer> GetOfferWithItemsAsync(int offerId)
    {
        return await _dbContext.Offers
            .Include(o => o.OfferConditions)
            .Include(o => o.OfferItems)
            .FirstOrDefaultAsync(o => o.OfferId == offerId);
    }

    public async Task<List<Offer>> GetActiveOffersAsync(int branchId, DateTime billDate, TimeSpan parsedBillTime, decimal billTotal)
    {
        return await _dbContext.Offers
            .Include(o => o.OfferConditions)
            .Include(o => o.OfferItems)
            .Where(o => o.Status == "Active" &&
                        o.IsAutoApply &&
                        !o.IsCouponRequired &&
                        o.StartDate <= billDate &&
                        o.EndDate >= billDate &&
                        (o.StartTime == null || o.StartTime <= parsedBillTime) &&
                        (o.EndTime == null || o.EndTime >= parsedBillTime) &&
                        (o.MinBillAmount == null || o.MinBillAmount <= billTotal))
            .OrderBy(o => o.Priority)
            .ToListAsync();
    }

    ~OfferRepository() { _repository = null; }
}
