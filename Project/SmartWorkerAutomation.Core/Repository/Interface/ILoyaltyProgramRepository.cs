using SmartWorkerAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface ILoyaltyProgramRepository
{
    Task<LoyaltyProgram> GetByIdAsync(int id);
    Task<IEnumerable<LoyaltyProgram>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Insert(LoyaltyProgram entity);
    void Update(LoyaltyProgram entity);
    Task<LoyaltyProgram> GetActiveProgramAsync();
}
