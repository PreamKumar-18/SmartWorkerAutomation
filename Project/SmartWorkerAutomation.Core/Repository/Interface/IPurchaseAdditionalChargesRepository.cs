using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IPurchaseAdditionalChargesRepository
{
    Task<PurchaseAdditionalCharge> GetByIdAsync(int id);
    Task<IEnumerable<PurchaseAdditionalCharge>> GetAllAsync();
    void Insert(PurchaseAdditionalCharge entity);
    void Update(PurchaseAdditionalCharge entity);
    void Delete(PurchaseAdditionalCharge entity);
}
