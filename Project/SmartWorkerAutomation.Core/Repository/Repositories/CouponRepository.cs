using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<Coupon> _repository;

    public CouponRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext;
        _repository = new GenericRepository<Coupon>(dbContext);
    }

    public async Task<Coupon> GetByIdAsync(int id) => await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<Coupon>> GetPaginatedAsync(Paging pagingConfig)
    {
        return await _repository.GetPageinatedDataAsync(pagingConfig, filters: (System.Linq.Expressions.Expression<System.Func<Coupon, bool>>)null);
    }

    public void Insert(Coupon entity) => _repository.Insert(entity);
    public void Update(Coupon entity) => _repository.Update(entity);

    public async Task<Coupon> GetByCodeAsync(string code)
    {
        return await _dbContext.Coupons.FirstOrDefaultAsync(c => c.CouponCode == code);
    }

    ~CouponRepository() { _repository = null; }
}
