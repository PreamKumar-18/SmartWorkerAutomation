using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ICompanyRepository
{
    Task<Company> GetByIdAsync(int id);
    Task<IEnumerable<Company>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(Company entity);
    void Update(Company entity);
}
