using System.Collections.Generic;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail);
    Task<int> RegisterUserViaFunctionAsync(User user);
    Task<int> UpdateUserViaFunctionAsync(User user);
    Task<int> ChangePasswordViaFunctionAsync(string username, string newPasswordHash, string updatedBy);
    Task<User?> GetByUserIDAsync(int userID);

    /// <summary>
    /// Persists the category allowlist for a 'User'-role account. Not routed
    /// through register_user/update_user since those DB functions predate
    /// this column and we don't have their source to extend safely.
    /// Table name is "User" (singular, capitalized) - both a reserved word
    /// and mixed-case in Postgres, so the implementation must double-quote
    /// it with exact casing preserved.
    /// </summary>
    Task<int> UpdateAllowedCategoriesAsync(int userId, string[]? categories);

    Task<IEnumerable<User>> GetAllUsersAsync();

    /// <summary>
    /// Upserts a push-notification device registration for a user, keyed on
    /// (user_id, device_id). Requires the user_device table - see
    /// RegisterDeviceAsync in UserService for the CREATE TABLE script.
    /// </summary>
    Task<int> UpsertDeviceAsync(int userId, RegisterDeviceRequest request);
}
