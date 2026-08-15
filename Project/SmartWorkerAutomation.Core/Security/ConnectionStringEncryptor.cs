using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SmartWorkerAutomation.Core.Security;

public class ConnectionStringEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public ConnectionStringEncryptor(IConfiguration configuration)
    {
        var keyBase64 = configuration["PrimaryDB:ConnectionStringEncryptionKey"]
            ?? throw new InvalidOperationException(
                "PrimaryDB:ConnectionStringEncryptionKey is not configured. " +
                "Generate one with: openssl rand -base64 32");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"PrimaryDB:ConnectionStringEncryptionKey must decode to 32 bytes (AES-256), got {_key.Length}.");
        }
    }

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var packed = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, packed, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, packed, NonceSize + ciphertext.Length, TagSize);

        return Convert.ToBase64String(packed);
    }

    public string Decrypt(string encryptedBase64)
    {
        var packed = Convert.FromBase64String(encryptedBase64);
        if (packed.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException("Encrypted connection string is malformed (too short).");
        }

        var nonce = packed[..NonceSize];
        var tag = packed[^TagSize..];
        var ciphertext = packed[NonceSize..^TagSize];
        var plaintextBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
