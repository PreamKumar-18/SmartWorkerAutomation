using System.Collections.Generic;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public interface IUserService
{
    /// <summary>
    /// Admin/SuperAdmin-authenticated user creation. Enforces that an
    /// 'Admin' creator can only create 'User' accounts - only a
    /// 'SuperAdmin' may create Admin or SuperAdmin accounts.
    /// </summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, int orgId, int roleId);
    Task<AuthResponse> CreateUserAsync(RegisterRequest request, string creatorRoleName, int creatorOrgId, int roleId);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request);
    Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, int orgId);
    Task<IEnumerable<User>> GetAllUsersAsync();

    /// <summary>
    /// Records/updates the calling user's device + push token, called right
    /// after login from web or mobile. userId comes from the authenticated
    /// JWT, not the request body, so a device can't be registered against a
    /// different user's account.
    /// </summary>
    Task<DeviceRegistrationResponse> RegisterDeviceAsync(int userId, RegisterDeviceRequest request);
    // IUserService
    Task<IEnumerable<User>> GetUsersEnquiryAsync(int requestingUserId, string requestingUserRole, int branchId = 0, string? sortColumn = null, string? sortDir = null, string? filtersJson = null, int? page = null, int? pageSize = null);
}
