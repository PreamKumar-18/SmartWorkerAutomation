using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Dapper;

namespace SmartWorkerAutomation.Core.Repository.Automation;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(DbConnectionFactory connectionFactory, IQueryStore queryStore) : base(connectionFactory, queryStore)
    {
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("User:GetByUsernameOrEmail");
        return await connection.QuerySingleOrDefaultAsync<User>(query, new { p_val = usernameOrEmail });
    }

    public async Task<User?> GetByUserIDAsync(int userID)
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("User:GetByUserId");
        return await connection.QuerySingleOrDefaultAsync<User>(query, new { p_userid = userID });
    }

    public async Task<int> RegisterUserViaFunctionAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = _queryStore.Get("User:RegisterUserViaFunction");

        var parameters = new
        {
            p_email = user.Email,
            p_phone = user.Phone,
            p_username = user.Username,
            p_password = user.Password,
            p_redirecturl = user.RedirectUrl,
            p_usertypeid = user.UserTypeId,
            p_createdby = user.CreatedBy
        };

        var userId = await connection.ExecuteScalarAsync<int>(query, parameters);
        user.UserId = userId;
        return userId;
    }

    public async Task<int> UpdateUserViaFunctionAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = _queryStore.Get("User:UpdateUserViaFunction");

        var parameters = new
        {
            p_userid = user.UserId,
            p_email = user.Email,
            p_phone = user.Phone,
            p_username = user.Username,
            p_redirecturl = user.RedirectUrl,
            p_usertypeid = user.UserTypeId,
            p_updatedby = user.UpdatedBy
        };

        return await connection.ExecuteScalarAsync<int>(query, parameters);
    }

    public async Task<int> ChangePasswordViaFunctionAsync(string username, string newPasswordHash, string updatedBy)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = _queryStore.Get("User:ChangePasswordViaFunction");

        var parameters = new
        {
            p_username = username,
            p_newpassword = newPasswordHash,
            p_updatedby = updatedBy
        };

        return await connection.ExecuteScalarAsync<int>(query, parameters);
    }

    public async Task<int> UpdateAllowedCategoriesAsync(int userId, string[]? categories)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = _queryStore.Get("User:UpdateAllowedCategories");

        var parameters = new
        {
            p_userid = userId,
            p_categories = categories
        };

        return await connection.ExecuteAsync(query, parameters);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var query = _queryStore.Get("User:GetAllUsers");
        return await connection.QueryAsync<User>(query);
    }

    public async Task<int> UpsertDeviceAsync(int userId, RegisterDeviceRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();

        var query = _queryStore.Get("User:UpsertDevice");

        var parameters = new
        {
            p_userid = userId,
            p_deviceid = request.DeviceId,
            p_pushtoken = request.PushToken,
            p_platform = request.Platform,
            p_devicemodel = request.DeviceModel,
            p_osversion = request.OsVersion,
            p_appversion = request.AppVersion
        };

        return await connection.ExecuteAsync(query, parameters);
    }
}
