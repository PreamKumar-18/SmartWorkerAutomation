using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartWorkerAutomation.Common.Automation;
using SmartWorkerAutomation.DataProvider.Interface.Automation;
using SmartWorkerAutomation.DataProvider.Service.Automation;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ITokenEncryptionService _tokenEncryptionService;

    public TokenService(IConfiguration configuration, ITokenEncryptionService tokenEncryptionService)
    {
        _configuration = configuration;
        _tokenEncryptionService = tokenEncryptionService;
    }

    public string GenerateToken(User user, UserInfo masterUserInfo, IEnumerable<UserBranchSummary>? branches)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var keyVal = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var durationMinutes = double.TryParse(jwtSettings["DurationInMinutes"], out var minutes) ? minutes : 60;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyVal));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.RoleName),
            //new Claim("accesstype", user.AccessTypeName),
            new Claim("orgid", masterUserInfo.OrgId.ToString())
        };

        if (user.AllowedCategories is { Length: > 0 })
        {
            claims.Add(new Claim("categories", string.Join(",", user.AllowedCategories)));
        }
        if (branches is not null)
        {
            var branchList = branches.ToList();
            if (branchList.Count > 0)
            {
                claims.Add(new Claim("branchids", string.Join(",", branchList.Select(b => b.BranchId))));

                var primary = branchList.FirstOrDefault(b => b.IsPrimary) ?? branchList.First();
                claims.Add(new Claim("primarybranchid", primary.BranchId.ToString()));
            }
        }
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(durationMinutes),
            signingCredentials: credentials);
        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Encrypt before returning - client stores/sends this opaque string,
        // never the raw JWT. CustomTokenAuthenticationHandler decrypts it back
        // on every subsequent request.
        return _tokenEncryptionService.Encrypt(rawToken);
    }
}
