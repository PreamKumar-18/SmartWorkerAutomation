using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface
{
    public interface ITaxMasterRepository
    {
        Task<TaxMaster> GetByIdAsync(int id);
        Task<IEnumerable<TaxMaster>> GetPaginatedAsync(Paging paging);
        void Insert(TaxMaster entity);
        void Update(TaxMaster entity);

    }
}
