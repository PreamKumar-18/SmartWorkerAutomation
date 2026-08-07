using SmartWorkerAutomation.Core.Models;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IUserRepository
{
    Task<User> GetUser(List<Expression<Func<User, bool>>> expression);
    Task<User?> GetUserById(int userId);  
    Task<User> GetUserByPhoneNumber(string phoneNumber);
    Task<IEnumerable<User>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig);
    void Add(User user);
    void Update(User user);
}
