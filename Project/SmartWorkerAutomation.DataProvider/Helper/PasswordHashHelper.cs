using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace SmartWorkerAutomation.DataProvider.Helper;

public static class PasswordHashHelper
{
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
}
