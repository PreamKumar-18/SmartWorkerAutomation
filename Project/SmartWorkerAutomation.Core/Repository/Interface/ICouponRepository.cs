using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ICouponRepository
{
    Task<Coupon> GetByIdAsync(int id);
    Task<IEnumerable<Coupon>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Coupon entity);
    void Update(Coupon entity);
    Task<Coupon> GetByCodeAsync(string code);
}
