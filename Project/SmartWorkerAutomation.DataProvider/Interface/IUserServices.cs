using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.DTOs.UserDTO;
using SmartWorkerAutomation.Common.LoginDTO;

namespace SmartWorkerAutomation.DataProvider.Interface;

public interface IUserServices 
{
    Task<LoginRsDTO> LoginWithOTP(CustomerVerifyOTP customerVerify, string ipAddress);
    Task<LoginRsDTO> Login(Login customerVerify, string ipAddress);
    Task<LoginRsDTO> RefreshTokenAsync(string refreshToken, string ipAddress); 
    
    // CRUD Operations appended logically identically to decoupled master modules
    Task<GenericResponse<UserResponseDto>> CreateAsync(UserCreateDto dto);
    Task<GenericResponse<UserResponseDto>> UpdateAsync(UserUpdateDto dto);
    Task<GenericPaginatedRes<List<UserResponseDto>>> GetDetailsAsync(int pageIndex, int pageSize, string sortBy, bool sortAsc);
    Task<GenericResponse<UserResponseDto>> GetByIdAsync(int id);
    Task<GenericResponse<bool>> SoftDeleteAsync(int id);
}
