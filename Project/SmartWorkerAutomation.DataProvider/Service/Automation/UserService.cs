using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.Core.Repository.Automation;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public UserService(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // 1. Check if user already exists by email
        var existingUser = await _userRepository.GetByUsernameOrEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new AuthResponse { Success = false, Message = "Email already registered." };
        }

        // Check if user already exists by username
        existingUser = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
        if (existingUser != null)
        {
            return new AuthResponse { Success = false, Message = "Username already taken." };
        }

        // 2. Hash password securely
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 3. Create user object
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

        // 4. Save user using database stored function
        try
        {
            await _userRepository.RegisterUserViaFunctionAsync(user);

            // 5. Persist category allowlist - only meaningful for the 'User'
            // role. Admin/SuperAdmin ignore this even if the caller sent it.
            if (user.UserTypeId == UserTypeIds.User && request.AllowedCategories is { Length: > 0 })
            {
                await _userRepository.UpdateAllowedCategoriesAsync(user.UserId, request.AllowedCategories);
                user.AllowedCategories = request.AllowedCategories;
            }

            user.Password = string.Empty; // Hide hash before returning
            return new AuthResponse { Success = true, Message = "User registered successfully.", User = user };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> CreateUserAsync(RegisterRequest request, string creatorRoleName)
    {
        bool creatorIsSuperAdmin = string.Equals(creatorRoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        bool creatorIsAdmin = string.Equals(creatorRoleName, "Admin", StringComparison.OrdinalIgnoreCase);

        if (!creatorIsSuperAdmin && !creatorIsAdmin)
        {
            return new AuthResponse { Success = false, Message = "You are not authorized to create users." };
        }

        // Admins may only create 'User' accounts; only a SuperAdmin can
        // create Admin or SuperAdmin accounts.
        if (creatorIsAdmin && request.UserTypeId != UserTypeIds.User)
        {
            return new AuthResponse { Success = false, Message = "Admins can only create User accounts." };
        }

        return await RegisterAsync(request);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllUsersAsync();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // 1. Fetch user
        var user = await _userRepository.GetByUsernameOrEmailAsync(request.UsernameOrEmail);
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "Invalid credentials." };
        }

        // 2. Verify password hash
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
        if (!isPasswordValid)
        {
            return new AuthResponse { Success = false, Message = "Invalid credentials." };
        }

        user.Password = string.Empty; // Hide hash before returning
        var token = _tokenService.GenerateToken(user);
        return new AuthResponse { Success = true, Message = "Login successful.", User = user, Token = token };
    }

    public async Task<AuthResponse> UpdateUserAsync(UpdateUserRequest request)
    {
        // 1. Verify user exists - GetByUserIDAsync (calls get_user_by_id()),
        // not the generic GetByIdAsync, which assumes a column literally
        // named "Id" and 404s against "User" (real PK column is "UserId").
        var currentUser = await _userRepository.GetByUserIDAsync(request.UserId);
        if (currentUser == null)
        {
            return new AuthResponse { Success = false, Message = "User not found." };
        }

        // 2. Check conflicts (email and username)
        var userWithEmail = await _userRepository.GetByUsernameOrEmailAsync(request.Email);
        if (userWithEmail != null && userWithEmail.UserId != request.UserId)
        {
            return new AuthResponse { Success = false, Message = "Email is already in use by another user." };
        }

        var userWithUsername = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
        if (userWithUsername != null && userWithUsername.UserId != request.UserId)
        {
            return new AuthResponse { Success = false, Message = "Username is already in use by another user." };
        }

        // 3. Update properties
        currentUser.Email = request.Email;
        currentUser.Phone = request.Phone;
        currentUser.Username = request.Username;
        currentUser.RedirectUrl = request.RedirectUrl;
        currentUser.UserTypeId = request.UserTypeId;
        currentUser.UpdatedBy = request.UpdatedBy ?? "System";

        // 4. Execute stored function
        try
        {
            await _userRepository.UpdateUserViaFunctionAsync(currentUser);

            // Persist category allowlist - only meaningful for the 'User' role.
            if (currentUser.UserTypeId == UserTypeIds.User)
            {
                await _userRepository.UpdateAllowedCategoriesAsync(currentUser.UserId, request.AllowedCategories);
                currentUser.AllowedCategories = request.AllowedCategories;
            }

            currentUser.Password = string.Empty; // Hide password hash
            return new AuthResponse { Success = true, Message = "User updated successfully.", User = currentUser };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Update failed: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request)
    {
        // 1. Fetch user
        var user = await _userRepository.GetByUsernameOrEmailAsync(request.Username);
        if (user == null)
        {
            return new AuthResponse { Success = false, Message = "User not found." };
        }

        // 2. Verify old password
        bool isOldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password);
        if (!isOldPasswordValid)
        {
            return new AuthResponse { Success = false, Message = "Incorrect old password." };
        }

        // 3. Hash new password
        var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // 4. Execute database function
        try
        {
            await _userRepository.ChangePasswordViaFunctionAsync(request.Username, newHashedPassword, "System");
            user.Password = string.Empty; // Hide hash
            return new AuthResponse { Success = true, Message = "Password changed successfully.", User = user };
        }
        catch (Exception ex)
        {
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
