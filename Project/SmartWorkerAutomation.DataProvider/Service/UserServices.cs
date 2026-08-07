using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.JwtDTO;
using SmartWorkerAutomation.Common.LoginDTO;
using SmartWorkerAutomation.Common.Enum;
using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using SmartWorkerAutomation.Core.Repository.Repositories;
using SmartWorkerAutomation.DataProvider.Helper;
using SmartWorkerAutomation.DataProvider.Interface;
using SmartWorkerAutomation.Transformation.OrganizationTransformation;
using SmartWorkerAutomation.Common.DTOs.UserDTO;

namespace SmartWorkerAutomation.DataProvider.Service;

public class UserServices : IUserServices
{
    private readonly SmartWorkerAutomationContext _dbContext;

    private IUserRepository _userRepository { get; set; }
    private readonly ILogger<UserServices> _logger;
    private IRefreshTokenRepository _refreshTokenRepository { get; set; }
    private CacheAppSettings _appSettings { get; set; }

    public UserServices(SmartWorkerAutomationContext dbContext,IOptions<CacheAppSettings> appSettings, ILogger<UserServices> logger)
    {
        _dbContext = dbContext;
        _userRepository = new UserRepository(dbContext);
        _refreshTokenRepository = new RefreshTokenRepository(dbContext);
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task<LoginRsDTO> LoginWithOTP(CustomerVerifyOTP customerVerify, string ipAddress)
    {
        try
        {
            LoginRsDTO loginRs = new LoginRsDTO();

            var user = await _userRepository.GetUser(new() { x => x.MobileNumber == customerVerify.Phone || x.LoginName == customerVerify.Phone });

            if (user == null)
            {
                throw new Exception("User Not found");
            }

            AuthResponseDto authResponse = JWTHelper.GenerateNewTokens(user, _appSettings.JwtSettings);

            var refreshToken = new RefreshToken
            {
                Token = authResponse.RefreshToken,
                UserId = user.UserId.ToString(),
                Expires = DateTime.UtcNow.AddDays(_appSettings.JwtSettings.RefreshTokenLifetimeDays),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            await _refreshTokenRepository.LoginRefreshToken(refreshToken, user.UserId);

            loginRs.Token = authResponse.AccessToken;
            loginRs.ExpiryMinutes = authResponse.ExpiryMinutes;
            loginRs.RefreshToken = authResponse.RefreshToken;
            loginRs.User = UserTransformation.ToDto(user);
            return loginRs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoginUser method of UserServices");
            throw;
        }
    }

    public async Task<LoginRsDTO> Login(Login customerVerify, string ipAddress)
    {
        try
        {
            LoginRsDTO loginRs = new LoginRsDTO();

            var user = await _userRepository.GetUser(new() { x => x.LoginName == customerVerify.UserIdentifier || x.MobileNumber == customerVerify.UserIdentifier });

            if (user == null)
            {
                throw new Exception("User Not found");
            }

            if(!UserTransformation.VerifyPassword(user.PasswordHash, customerVerify.Pin))
            {
                throw new Exception("Incorrect Password");
            }

            AuthResponseDto authResponse = JWTHelper.GenerateNewTokens(user, _appSettings.JwtSettings);

            var refreshToken = new RefreshToken
            {
                Token = authResponse.RefreshToken,
                UserId = user.UserId.ToString(),
                Expires = DateTime.UtcNow.AddDays(_appSettings.JwtSettings.RefreshTokenLifetimeDays),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            await _refreshTokenRepository.LoginRefreshToken(refreshToken, user.UserId);

            loginRs.Token = authResponse.AccessToken;
            loginRs.ExpiryMinutes = authResponse.ExpiryMinutes;
            loginRs.RefreshToken = authResponse.RefreshToken;
            loginRs.User = UserTransformation.ToDto(user);
            return loginRs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoginUser method of UserServices");
            throw;
        }
    }

    public async Task<LoginRsDTO> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        var oldToken = await _refreshTokenRepository
            .GetRefreshToken(refreshToken, ipAddress, DateTime.UtcNow);

        if (oldToken == null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token");

        var userId = int.Parse(oldToken.UserId);


        var user = await _userRepository.GetUserById(userId);
        if (user == null)
            throw new UnauthorizedAccessException("User not found");

        // Generate new JWT + Refresh token
        var authResponse = JWTHelper.GenerateNewTokens(user, _appSettings.JwtSettings);

        var newRefreshToken = new RefreshToken
        {
            Token = authResponse.RefreshToken,
            Expires = DateTime.UtcNow.AddDays(
                _appSettings.JwtSettings.RefreshTokenLifetimeDays),
            Created = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            UserId = userId.ToString()
        };

        oldToken.ReplacedByToken = newRefreshToken.Token;

        await _refreshTokenRepository.LoginRefreshToken(newRefreshToken, userId);
        var expiresAt = DateTime.Now.AddDays(_appSettings.JwtSettings.RefreshTokenLifetimeDays);

        int expiryMinutes = (int)Math.Ceiling(
            (expiresAt - DateTime.Now).TotalMinutes
        );
        return new LoginRsDTO
        {
            Token = authResponse.AccessToken,
            RefreshToken = authResponse.RefreshToken,
            ExpiryMinutes = expiryMinutes,
            IsNewUser = false,
            User = UserTransformation.ToDto(user)
        };
    }

    public async Task<GenericResponse<UserResponseDto>> CreateAsync(UserCreateDto dto)
    {
        var entity = UserTransformation.ToEntity(dto);
        _userRepository.Add(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<UserResponseDto>(RSCodeEnum.Success, UserTransformation.ToDto(entity), "User explicitly created.");
    }

    public async Task<GenericResponse<UserResponseDto>> UpdateAsync(UserUpdateDto dto)
    {
        var entity = await _userRepository.GetUserById(dto.UserId);
        if (entity == null) return new GenericResponse<UserResponseDto>(RSCodeEnum.NoRecordFound, null, "Not found.");
        UserTransformation.UpdateEntity(entity, dto);
        _userRepository.Update(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<UserResponseDto>(RSCodeEnum.Success, UserTransformation.ToDto(entity), "Updated.");
    }

    public async Task<GenericPaginatedRes<List<UserResponseDto>>> GetDetailsAsync(int pageIndex, int pageSize, string sortBy, bool sortAsc)
    {
        var pagingConfig = new Paging { PageIndex = pageIndex, PageSize = pageSize, SortBy = sortBy, SortAsc = sortAsc };
        var paged = await _userRepository.GetPaginatedAsync(pagingConfig);
        if (paged == null || !System.Linq.Enumerable.Any(paged)) return new GenericPaginatedRes<List<UserResponseDto>>(RSCodeEnum.NoRecordFound, null, pagingConfig);
        return new GenericPaginatedRes<List<UserResponseDto>>(RSCodeEnum.Success, paged.Select( x => UserTransformation.ToDto(x)).ToList(), pagingConfig);
    }

    public async Task<GenericResponse<UserResponseDto>> GetByIdAsync(int id)
    {
        var entity = await _userRepository.GetUserById(id);
        if (entity == null) return new GenericResponse<UserResponseDto>(RSCodeEnum.NoRecordFound, null, "Not found.");
        return new GenericResponse<UserResponseDto>(RSCodeEnum.Success, UserTransformation.ToDto(entity));
    }

    public async Task<GenericResponse<bool>> SoftDeleteAsync(int id)
    {
        var entity = await _userRepository.GetUserById(id);
        if (entity == null) return new GenericResponse<bool>(RSCodeEnum.NoRecordFound, false, "Not found.");

        entity.IsActive = false;
        entity.UpdatedOn = DateTime.Now;
        _userRepository.Update(entity);
        await _dbContext.SaveChangesAsync();
        return new GenericResponse<bool>(RSCodeEnum.Success, true, "User deactivated gracefully.");
    }
}

