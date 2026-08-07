using SmartWorkerAutomation.Core.Models;

namespace SmartWorkerAutomation.Core.Repository.Interface;

public interface IRefreshTokenRepository
{
    Task LoginRefreshToken(RefreshToken refreshToken, int userId);
    Task<RefreshToken?> GetRefreshToken(string refreshToken, string ipAddress, DateTime currentTime);
}
