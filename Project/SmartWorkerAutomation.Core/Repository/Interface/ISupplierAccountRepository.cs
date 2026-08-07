using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ISupplierAccountRepository
{
    Task<SupplierAccount> GetByIdAsync(int id);
    Task<IEnumerable<SupplierAccount>> GetAllAsync();
    void Insert(SupplierAccount entity);
    void Update(SupplierAccount entity);
    void Delete(SupplierAccount entity);
}
