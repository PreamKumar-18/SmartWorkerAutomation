using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchaseActivityLogRepository
{
    void Insert(PurchaseActivityLog entity);
    IQueryable<PurchaseActivityLog> GetAllQueryable();
}
