using Microsoft.EntityFrameworkCore;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SmartWorkerAutomationContext _dbContext;
    private IGenericRepository<User> repository { get; set; }

    public UserRepository(SmartWorkerAutomationContext dbContext)
    {
        _dbContext = dbContext; 
        repository = new GenericRepository<User>(dbContext);
    }

    public async Task<User?> GetUserById(int userId)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<User?> GetUserByPhoneNumber(string phoneNumber)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.MobileNumber == phoneNumber);
    }

    public async Task<User> GetUser(List<Expression<Func<User, bool>>> expression)
    {
        return await repository.SearchTop1Async(expression);
    }

    public async Task<IEnumerable<User>> GetPaginatedAsync(SmartWorkerAutomation.Common.Common.Paging pagingConfig)
    {
        return await repository.GetPageinatedDataAsync(pagingConfig, filters: x => x.IsActive);
    }

    public void Add(User user) => repository.Insert(user);

    public void Update(User user) => repository.Update(user);
}
