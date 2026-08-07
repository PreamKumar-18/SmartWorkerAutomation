using SmartWorkerAutomation.Common.DTOs.UserDTO;

namespace SmartWorkerAutomation.Common.LoginDTO;

public class LoginRsDTO
{
    public bool IsNewUser { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public int ExpiryMinutes { get; set; }
    public UserResponseDto User { get; set; }
}