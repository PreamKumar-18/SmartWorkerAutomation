using SmartWorkerAutomation.DataProvider.Interface;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SmartWorkerAutomation.Core.Interface;

namespace SmartWorkerAutomation.DataProvider.Service;

public class UserContextServices : IUserContextServices, ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContextServices(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public int GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user == null || !user.Identity!.IsAuthenticated)
            throw new UnauthorizedAccessException("User is not authenticated");

        var claim =
            user.FindFirst("UserId") ??
            user.FindFirst(ClaimTypes.NameIdentifier);

        if (claim == null || !int.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Invalid UserId claim");

        return userId;
    }

    public int? GetCurrentUserIdNullable()
    {
        try
        {
            return GetCurrentUserId();
        }
        catch
        {
            return null;
        }
    }

    public string GetCurrentUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user == null || !user.Identity!.IsAuthenticated)
            throw new UnauthorizedAccessException("User is not authenticated");

        return
            user.FindFirst(ClaimTypes.Name)?.Value ??
            user.FindFirst("UserName")?.Value ??
            string.Empty;
    }
}
