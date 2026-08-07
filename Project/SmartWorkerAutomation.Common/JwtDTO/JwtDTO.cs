namespace SmartWorkerAutomation.Common.JwtDTO;

public class AuthResponseDto
{
    public int ExpiryMinutes { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}

public class RefreshTokenRequestDto
{
    public string Token { get; set; }
}

