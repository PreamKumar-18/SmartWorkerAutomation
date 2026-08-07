using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchaseStatusRepository
{
    Task<PurchaseStatus> GetByIdAsync(int id);
    void Insert(PurchaseStatus entity);
    void Update(PurchaseStatus entity);
    Task<List<PurchaseStatus>> GetPaginatedAsync(Paging pagingConfig, Expression<Func<PurchaseStatus, bool>> filter = null);
}
