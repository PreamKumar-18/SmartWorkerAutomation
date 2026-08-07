using SmartWorkerAutomation.Core.DBContext;
using SmartWorkerAutomation.Core.Generic;
using SmartWorkerAutomation.Core.Models;
using SmartWorkerAutomation.Core.Repository.Interface;
using System.Net;

namespace SmartWorkerAutomation.Core.Repository.Repositories;

public class RefreshTokenRepository(SmartWorkerAutomationContext dbContext) : IRefreshTokenRepository
{
    private IGenericRepository<RefreshToken> repository { get; set; }
        = new GenericRepository<RefreshToken>(dbContext);

    public async Task LoginRefreshToken(RefreshToken refreshToken, int userId)
    {
        // One active refresh token per user
        await repository.RemoveAllAsync(x => x.UserId == userId.ToString());

        refreshToken.UserId = userId.ToString();
        repository.Insert(refreshToken);

        await repository.SaveChangesAsync();
    }

    public async Task<RefreshToken?> GetRefreshToken(string token, string ipAddress, DateTime currentTime)
    {
        var refreshToken = await repository.SearchTop1Async(x => x.Token == token);

        if (refreshToken == null ||
            refreshToken.Expires <= currentTime ||
            refreshToken.Revoked != null)
            return null;

        // revoke old token
        refreshToken.Revoked = currentTime;
        refreshToken.RevokedByIp = ipAddress;

        await repository.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<bool> ReplaceRefreshToken(int userId)
    {
        await repository.RemoveAllAsync(o => o.UserId == userId.ToString());
        var result = await repository.SaveChangesAsync();
        return result > 0;
    }
}


 
