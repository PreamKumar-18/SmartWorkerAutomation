using Dapper;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;
using SmartWorkerAutomation.DataProvider.Service.Automation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ITenantResolverService _tenantResolverService;
    private readonly IQueryStore _queryStore;
    private readonly IMasterAuthRepository _masterAuthRepository;
    private readonly DbConnectionFactory _connectionFactory;

    public UserService(IUserRepository userRepository, ITokenService tokenService, ITenantResolverService tenantResolverService, 
        IQueryStore queryStore, IMasterAuthRepository masterAuthRepository, DbConnectionFactory connectionFactory)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _tenantResolverService = tenantResolverService;
        _queryStore = queryStore;
        _masterAuthRepository = masterAuthRepository;
        _connectionFactory = connectionFactory;
    }

    //public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    //{
    //    // 1. Check if user already exists by email
    //    var existingUser = await _userRepository.GetByUsernameOrEmailAsync(request.Email);
    //    if (existingUser != null)
    //    {
    //        return new AuthResponse { Success = false, Message = "Email already registered." };
    //    }

    //    // Check if user already exists by username
    //    existingUser = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
    //    if (existingUser != null)
    //    {
    //        return new AuthResponse { Success = false, Message = "Username already taken." };
    //    }

    //    // 2. Hash password securely
    //    var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

    //    // 3. Create user object
    //    var user = new User
    //    {
    //        Email = request.Email,
    //        Phone = request.Phone,
    //        Username = request.Username,
    //        Password = hashedPassword,
    //        RedirectUrl = request.RedirectUrl,
    //        UserTypeId = request.UserTypeId,
    //        CreatedBy = "System",
    //        UpdatedBy = "System"
    //    };

    //    // 4. Save user using database stored function
    //    try
    //    {
    //        await _userRepository.RegisterUserViaFunctionAsync(user);

    //        // 5. Persist category allowlist - only meaningful for the 'User'
    //        // role. Admin/SuperAdmin ignore this even if the caller sent it.
    //        if (user.UserTypeId == UserTypeIds.User && request.AllowedCategories is { Length: > 0 })
    //        {
    //            await _userRepository.UpdateAllowedCategoriesAsync(user.UserId, request.AllowedCategories);
    //            user.AllowedCategories = request.AllowedCategories;
    //        }

    //        user.Password = string.Empty; // Hide hash before returning
    //        return new AuthResponse { Success = true, Message = "User registered successfully.", User = user };
    //    }
    //    catch (Exception ex)
    //    {
    //        return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
    //    }
    //}

    //public async Task<AuthResponse> CreateUserAsync(RegisterRequest request, string creatorRoleName)
    //{
    //    bool creatorIsSuperAdmin = string.Equals(creatorRoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
    //    bool creatorIsAdmin = string.Equals(creatorRoleName, "Admin", StringComparison.OrdinalIgnoreCase);

    //    if (!creatorIsSuperAdmin && !creatorIsAdmin)
    //    {
    //        return new AuthResponse { Success = false, Message = "You are not authorized to create users." };
    //    }

    //    // Admins may only create 'User' accounts; only a SuperAdmin can
    //    // create Admin or SuperAdmin accounts.
    //    if (creatorIsAdmin && request.UserTypeId != UserTypeIds.User)
    //    {
    //        return new AuthResponse { Success = false, Message = "Admins can only create User accounts." };
    //    }

    //    return await RegisterAsync(request);
    //}

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllUsersAsync();
    }

    //public async Task<AuthResponse> LoginAsync(LoginRequest request)
    //{
    //    // 1. Fetch user
    //    var user = await _userRepository.GetByUsernameOrEmailAsync(request.UsernameOrEmail);
    //    if (user == null)
    //    {
    //        return new AuthResponse { Success = false, Message = "Invalid credentials." };
    //    }

    //    // 2. Verify password hash
    //    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
    //    if (!isPasswordValid)
    //    {
    //        return new AuthResponse { Success = false, Message = "Invalid credentials." };
    //    }

    //    user.Password = string.Empty; // Hide hash before returning
    //    var token = _tokenService.GenerateToken(user);
    //    return new AuthResponse { Success = true, Message = "Login successful.", User = user, Token = token };
    //}

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 1. Authenticate against the master DB (userinfo) - we don't know
        // which tenant DB to use until this resolves.
        var tenantContext = await _tenantResolverService.ResolveByEmailAsync(request.UsernameOrEmail);
        if (tenantContext is null)
        {
            return new AuthResponse { Success = false, Message = "Invalid credentials." };
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, tenantContext.User.PasswordHash);
        if (!isPasswordValid)
        {
            return new AuthResponse { Success = false, Message = "Invalid credentials." };
        }

        // 2. Look up the matching tenant-side User row for UserId/AllowedCategories etc.
        using var tenantConnection = new NpgsqlConnection(tenantContext.DecryptedConnectionString);
        var tenantUserQuery = _queryStore.Get("User:GetByUsernameOrEmail");
        var tenantUser = await tenantConnection.QuerySingleOrDefaultAsync<User>(
            tenantUserQuery, new { p_val = tenantContext.User.Email });

        if (tenantUser is null)
        {
            return new AuthResponse { Success = false, Message = "User account is not fully provisioned. Contact support." };
        }

        // NEW - fetch this user's branch access (or all branches, if SuperAdmin)
        bool isSuperAdmin = string.Equals(tenantContext.User.RoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        var branchQueryKey = isSuperAdmin ? "Branch:GetAllActiveBranches" : "Branch:GetBranchesForUser";
        var branchSql = _queryStore.Get(branchQueryKey);
        var branches = await tenantConnection.QueryAsync<UserBranchSummary>(branchSql, new { UserId = tenantUser.UserId });

        tenantUser.Password = string.Empty;
        var token = _tokenService.GenerateToken(tenantUser, tenantContext.User, branches);

        return new AuthResponse { Success = true, Message = "Login successful.", User = tenantUser, Token = token, Branches = branches.ToList() };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, int orgId, int roleId)
    {
        // 1. Resolve the tenant connection for this org
        var tenantConnectionString = await _tenantResolverService.GetTenantConnectionStringAsync(orgId);
        if (tenantConnectionString is null)
        {
            return new AuthResponse { Success = false, Message = "Could not resolve tenant database for this organisation." };
        }

        // 2. Check master userinfo for existing email (global uniqueness -
        // userinfo.email has a UNIQUE constraint across ALL orgs)
        var existingMasterUser = await _masterAuthRepository.GetUserByEmailAsync(request.Email);
        if (existingMasterUser != null)
        {
            return new AuthResponse { Success = false, Message = "Email already registered." };
        }

        // 3. Also check tenant-side uniqueness (username/email within this org's User table)
        using var tenantConnection = new NpgsqlConnection(tenantConnectionString);
        var tenantCheckQuery = _queryStore.Get("User:GetByUsernameOrEmail");
        var existingTenantUser = await tenantConnection.QuerySingleOrDefaultAsync<User>(
            tenantCheckQuery, new { p_val = request.Email });
        if (existingTenantUser != null)
        {
            return new AuthResponse { Success = false, Message = "Email already registered in this organisation." };
        }

        existingTenantUser = await tenantConnection.QuerySingleOrDefaultAsync<User>(
            tenantCheckQuery, new { p_val = request.Username });
        if (existingTenantUser != null)
        {
            return new AuthResponse { Success = false, Message = "Username already taken." };
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 4. Insert into master userinfo FIRST - this is where the global
        // email-uniqueness constraint actually lives (DB-enforced), so if this
        // fails (race condition past the check above), nothing tenant-side has
        // been written yet.
        int masterUserId;
        try
        {
            masterUserId = await _masterAuthRepository.InsertUserInfoAsync(
                orgId, request.Username, request.Email, hashedPassword, request.AllowedCategories);
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }

        // 5. Insert into the tenant DB's own User table
        var user = new User
        {
            Email = request.Email,
            Phone = request.Phone,
            Username = request.Username,
            Password = hashedPassword,
            RedirectUrl = request.RedirectUrl,
            UserTypeId = request.UserTypeId,
            CreatedBy = "System",
            UpdatedBy = "System"
        };

        try
        {
            var registerSql = _queryStore.Get("User:RegisterUserViaFunction");
            var tenantUserId = await tenantConnection.ExecuteScalarAsync<int>(registerSql, new
            {
                p_email = user.Email,
                p_phone = user.Phone,
                p_username = user.Username,
                p_password = user.Password,
                p_redirecturl = user.RedirectUrl,
                p_usertypeid = user.UserTypeId,
                p_createdby = user.CreatedBy,
            });
            user.UserId = tenantUserId;

            if (request.BranchIds is { Length: > 0 })
            {
                var insertBranchSql = _queryStore.Get("User:InsertUserBranch");
                foreach (var branchId in request.BranchIds.Distinct())
                {
                    var isPrimary = request.PrimaryBranchId.HasValue
                        ? branchId == request.PrimaryBranchId.Value
                        : branchId == request.BranchIds[0]; // default to first if no explicit primary given

                    try
                    {
                        await tenantConnection.ExecuteAsync(insertBranchSql, new { UserId = user.UserId, BranchId = branchId, IsPrimary = isPrimary });
                    }
                    catch (Exception ex)
                    {
                        // A bad branchId (doesn't exist in THIS org's branch table)
                        // shouldn't fail the whole user creation - log and continue,
                        // same "best-effort secondary write" pattern used elsewhere
                        // (e.g. FinalizeAsync in ReminderSendBackgroundService).
                        return new AuthResponse
                        {
                            Success = false,
                            Message = $"Failed to map userid {user.UserId} to branchid {branchId}. Ex: {ex.Message}"
                        };
                    }
                }
            }

            if (user.UserTypeId == UserTypeIds.User && request.AllowedCategories is { Length: > 0 })
            {
                await _userRepository.UpdateAllowedCategoriesAsync(user.UserId, request.AllowedCategories);
                user.AllowedCategories = request.AllowedCategories;
            }

            user.Password = string.Empty;
            return new AuthResponse { Success = true, Message = "User registered successfully.", User = user };
        }
        catch (Exception ex)
        {
            // Master userinfo row now exists with no matching tenant User row -
            // logged clearly so it can be manually reconciled; not automatically
            // rolled back since master/tenant are separate DBs (no distributed
            // transaction, same tradeoff as OrganisationOnboardingService).
            //_logger.LogError(ex, "RegisterAsync: master userinfo id {MasterUserId} created for orgid {OrgId}, but tenant User insert failed.", masterUserId, orgId);
            return new AuthResponse { Success = false, Message = $"Registration failed at tenant database step: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> CreateUserAsync(RegisterRequest request, string creatorRoleName, int creatorOrgId, int roleId)
    {
        bool creatorIsSuperAdmin = string.Equals(creatorRoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        bool creatorIsAdmin = string.Equals(creatorRoleName, "Admin", StringComparison.OrdinalIgnoreCase);

        if (!creatorIsSuperAdmin && !creatorIsAdmin)
        {
            return new AuthResponse { Success = false, Message = "You are not authorized to create users." };
        }

        if (creatorIsAdmin && request.UserTypeId != UserTypeIds.User)
        {
            return new AuthResponse { Success = false, Message = "Admins can only create User accounts." };
        }

        // New user is always created under the CREATOR's own org - an Admin
        // can never create a user in a different organisation.
        return await RegisterAsync(request, creatorOrgId, roleId);
    }
    //public async Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request)
    //{
    //    // 1. Verify user exists - GetByUserIDAsync (calls get_user_by_id()),
    //    // not the generic GetByIdAsync, which assumes a column literally
    //    // named "Id" and 404s against "User" (real PK column is "UserId").
    //    var currentUser = await _userRepository.GetByUserIDAsync(request.UserId);
    //    if (currentUser == null)
    //    {
    //        return new AuthResponse { Success = false, Message = "User not found." };
    //    }

    //    // 2. Check conflicts (email and username)
    //    var userWithEmail = await _userRepository.GetByUsernameOrEmailAsync(request.Email);
    //    if (userWithEmail != null && userWithEmail.UserId != request.UserId)
    //    {
    //        return new AuthResponse { Success = false, Message = "Email is already in use by another user." };
    //    }

    //    var userWithUsername = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
    //    if (userWithUsername != null && userWithUsername.UserId != request.UserId)
    //    {
    //        return new AuthResponse { Success = false, Message = "Username is already in use by another user." };
    //    }

    //    // 3. Update properties
    //    currentUser.Email = request.Email;
    //    currentUser.Phone = request.Phone;
    //    currentUser.Username = request.Username;
    //    currentUser.RedirectUrl = request.RedirectUrl;
    //    currentUser.UserTypeId = request.UserTypeId;
    //    currentUser.UpdatedBy = request.UpdatedBy ?? "System";

    //    // 4. Execute stored function
    //    try
    //    {
    //        await _userRepository.UpdateUserViaFunctionAsync(currentUser);

    //        // Persist category allowlist - only meaningful for the 'User' role.
    //        if (currentUser.UserTypeId == UserTypeIds.User)
    //        {
    //            await _userRepository.UpdateAllowedCategoriesAsync(currentUser.UserId, request.AllowedCategories);
    //            currentUser.AllowedCategories = request.AllowedCategories;
    //        }

    //        currentUser.Password = string.Empty; // Hide password hash
    //        return new AuthResponse { Success = true, Message = "User updated successfully.", User = currentUser };
    //    }
    //    catch (Exception ex)
    //    {
    //        return new AuthResponse { Success = false, Message = $"Update failed: {ex.Message}" };
    //    }
    //}

    public async Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request)
    {
        var currentUser = await _userRepository.GetByUserIDAsync(request.UserId);
        if (currentUser == null)
        {
            return new AuthResponse { Success = false, Message = "User not found." };
        }

        // Global email-uniqueness check against master, not just this tenant.
        if (!string.Equals(currentUser.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var conflictingMasterUser = await _masterAuthRepository.GetUserByEmailAsync(request.Email);
            if (conflictingMasterUser != null)
            {
                return new AuthResponse { Success = false, Message = "Email is already in use by another user." };
            }
        }

        var userWithUsername = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
        if (userWithUsername != null && userWithUsername.UserId != request.UserId)
        {
            return new AuthResponse { Success = false, Message = "Username is already in use by another user." };
        }

        var oldEmail = currentUser.Email;

        currentUser.Email = request.Email;
        currentUser.Phone = request.Phone;
        currentUser.Username = request.Username;
        currentUser.RedirectUrl = request.RedirectUrl;
        currentUser.UserTypeId = request.UserTypeId;
        currentUser.UpdatedBy = request.UpdatedBy ?? "System";

        try
        {
            await _userRepository.UpdateUserViaFunctionAsync(currentUser);

            // Keep master userinfo's email/username in sync if either changed -
            // login resolves by email against master, so a stale master email
            // would silently break login with the new address.
            if (!string.Equals(oldEmail, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                var masterUser = await _masterAuthRepository.GetUserByEmailAsync(oldEmail);
                if (masterUser is not null)
                {
                    await _masterAuthRepository.UpdateEmailAndUsernameAsync(masterUser.Id, request.Email, request.Username); // NEW method
                }
            }

            if (currentUser.UserTypeId == UserTypeIds.User)
            {
                await _userRepository.UpdateAllowedCategoriesAsync(currentUser.UserId, request.AllowedCategories);
                currentUser.AllowedCategories = request.AllowedCategories;
            }

            currentUser.Password = string.Empty;
            return new AuthResponse { Success = true, Message = "User updated successfully.", User = currentUser };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Update failed: {ex.Message}" };
        }
    }

    //public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request)
    //{
    //    // 1. Fetch user
    //    var user = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
    //    if (user == null)
    //    {
    //        return new AuthResponse { Success = false, Message = "User not found." };
    //    }

    //    // 2. Verify old password
    //    bool isOldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password);
    //    if (!isOldPasswordValid)
    //    {
    //        return new AuthResponse { Success = false, Message = "Incorrect old password." };
    //    }

    //    // 3. Hash new password
    //    var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

    //    // 4. Execute database function
    //    try
    //    {
    //        await _userRepository.ChangePasswordViaFunctionAsync(request.Username, newHashedPassword, "System");
    //        user.Password = string.Empty; // Hide hash
    //        return new AuthResponse { Success = true, Message = "Password changed successfully.", User = user };
    //    }
    //    catch (Exception ex)
    //    {
    //        return new AuthResponse { Success = false, Message = $"Password change failed: {ex.Message}" };
    //    }
    //}

    public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, int orgId)
    {
        var user = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "User not found." };
        }

        // Verify against master's hash - that's what LoginAsync actually checks.
        var masterUser = await _masterAuthRepository.GetUserByEmailAsync(user.Email);
        if (masterUser is null)
        {
            return new AuthResponse { Success = false, Message = "User not found in master records." };
        }

        bool isOldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, masterUser.PasswordHash);
        if (!isOldPasswordValid)
        {
            return new AuthResponse { Success = false, Message = "Incorrect old password." };
        }

        var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Update BOTH - master (what login checks) and tenant (kept in sync so
        // nothing else reading tenant User.Password is left stale).
        try
        {
            await _masterAuthRepository.UpdatePasswordAsync(masterUser.Id, newHashedPassword); // NEW method, see below
            await _userRepository.ChangePasswordViaFunctionAsync(request.Username, newHashedPassword, "System");
            user.Password = string.Empty;
            return new AuthResponse { Success = true, Message = "Password changed successfully.", User = user };
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, "ChangePasswordAsync: password update failed for orgid {OrgId}, username {Username}.", orgId, request.Username);
            return new AuthResponse { Success = false, Message = $"Password change failed: {ex.Message}" };
        }
    }

    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(int userId, RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.PushToken))
        {
            return new DeviceRegistrationResponse { Success = false, Message = "DeviceId and PushToken are required." };
        }

        try
        {
            await _userRepository.UpsertDeviceAsync(userId, request);
            return new DeviceRegistrationResponse { Success = true, Message = "Device registered." };
        }
        catch (Exception ex)
        {
            return new DeviceRegistrationResponse { Success = false, Message = $"Device registration failed: {ex.Message}" };
        }
    }
}
