using SmartWorkerAutomation.Common.DTOs.UserDTO;
using SmartWorkerAutomation.Core.Models;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace SmartWorkerAutomation.Transformation.OrganizationTransformation;

public static class UserTransformation
{
    // Utility to natively generate hash inside transformation payload easily to not leak business mechanics.
    private static readonly int _iterations = 10000;
    private static readonly int _saltSize = 16; // 128 bits
    private static readonly int _hashSize = 32; // 256 bits

    public static string HashPassword(string password)
    {
        // Generate a random salt
        byte[] salt = new byte[_saltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive the hash
        byte[] hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: _iterations,
            numBytesRequested: _hashSize);

        // Combine salt and hash
        byte[] hashBytes = new byte[_saltSize + _hashSize];
        Array.Copy(salt, 0, hashBytes, 0, _saltSize);
        Array.Copy(hash, 0, hashBytes, _saltSize, _hashSize);

        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        byte[] hashBytes = Convert.FromBase64String(hashedPassword);

        // Extract salt
        byte[] salt = new byte[_saltSize];
        Array.Copy(hashBytes, 0, salt, 0, _saltSize);

        // Compute hash of provided password
        byte[] providedHash = KeyDerivation.Pbkdf2(
            password: providedPassword,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: _iterations,
            numBytesRequested: _hashSize);

        // Compare hashes
        for (int i = 0; i < _hashSize; i++)
        {
            if (hashBytes[i + _saltSize] != providedHash[i])
                return false;
        }
        return true;
    }

    public static User ToEntity(UserCreateDto dto)
    {
        return new User
        {
            BranchId = dto.BranchId,
            RoleId = dto.RoleId,
            UserCode = dto.UserCode,
            FullName = dto.FullName,
            MobileNumber = dto.MobileNumber,
            Email = dto.Email,
            LoginName = dto.LoginName,
            PasswordHash = HashPassword(dto.Password),
            PinCode = dto.PinCode,
            DefaultScreen = dto.DefaultScreen,
            IsActive = true,
            CreatedOn = System.DateTime.Now,
            UpdatedOn = System.DateTime.Now
        };
    }

    public static UserResponseDto ToDto(User entity)
    {
        return new UserResponseDto
        {
            UserId = entity.UserId,
            BranchId = entity.BranchId,
            RoleId = entity.RoleId,
            UserCode = entity.UserCode,
            FullName = entity.FullName,
            MobileNumber = entity.MobileNumber,
            Email = entity.Email,
            LoginName = entity.LoginName,
            PinCode = entity.PinCode,
            DefaultScreen = entity.DefaultScreen,
            IsActive = entity.IsActive,
            LastLoginAt = entity.LastLoginAt,
            CreatedOn = entity.CreatedOn
        };
    }

    public static void UpdateEntity(User entity, UserUpdateDto dto)
    {
        entity.BranchId = dto.BranchId;
        entity.RoleId = dto.RoleId;
        entity.UserCode = dto.UserCode;
        entity.FullName = dto.FullName;
        entity.MobileNumber = dto.MobileNumber;
        entity.Email = dto.Email;
        entity.LoginName = dto.LoginName;
        entity.PinCode = dto.PinCode;
        entity.DefaultScreen = dto.DefaultScreen;
        entity.UpdatedOn = System.DateTime.Now;
    }
}
