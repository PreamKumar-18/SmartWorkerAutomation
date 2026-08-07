using SmartWorkerAutomation.Common.Common;
using SmartWorkerAutomation.Common.JwtDTO;
using SmartWorkerAutomation.Core.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Helper;

public static class JWTHelper
{
    private static readonly string JWTSecretKey = "super_secret_key_1234567890efkhbjech_is_6565g_em54i85nnough_32_chars";

    public static AuthResponseDto GenerateNewTokens(User user, Jwtsettings jwtsettings)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(JWTSecretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new Claim[]
            {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.LoginName),
                    new Claim("Phone", user.MobileNumber.ToString() ?? string.Empty)
            }),

            Expires = DateTime.Now.AddMinutes(jwtsettings.ExpiryMinutes),
            Issuer = jwtsettings.Issuer,
            Audience = jwtsettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        var jwtToken = tokenHandler.WriteToken(token);

        // Debug output to verify token structure
        if (!jwtToken.Contains('.'))
        {
            throw new InvalidOperationException("Generated token is not a valid JWT (missing dots)");
        }
        var expiresAt = tokenDescriptor.Expires ?? DateTime.Now;

        int expiryMinutes = (int)Math.Ceiling(
            (expiresAt - DateTime.Now).TotalMinutes
        );
        return new AuthResponseDto
        {
            AccessToken = EncryptDecryptHelper.EncryptToken(jwtToken),
            ExpiryMinutes = expiryMinutes,
            RefreshToken = EncryptDecryptHelper.EncryptToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)))
        };
    }
}
