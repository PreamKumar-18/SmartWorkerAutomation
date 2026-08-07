using System.Security.Cryptography;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Helper;

public static class EncryptDecryptHelper
{
    private static readonly string EncryptionKey = "b14ca5898a4e4133bbce2ea2315a1916";
    private static readonly string EncryptionIV = "a1b2c3d4e5f6g7h8";

    public static string EncryptToken(string token)
    {
        var key = Encoding.UTF8.GetBytes(EncryptionKey); // Must be 32 chars

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var encryptedBytes = encryptor.TransformFinalBlock(tokenBytes, 0, tokenBytes.Length);

        // Combine IV and encrypted data
        var result = aes.IV.Concat(encryptedBytes).ToArray();

        return Convert.ToBase64String(result);
    }

    public async static Task<string> DecryptValue(string encryptedValue)
    {

        var fullCipher = Convert.FromBase64String(encryptedValue);

        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);

        var iv = new byte[16];
        Array.Copy(fullCipher, 0, iv, 0, iv.Length);

        var encryptedBytes = new byte[fullCipher.Length - iv.Length];
        Array.Copy(fullCipher, iv.Length, encryptedBytes, 0, encryptedBytes.Length);

        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        using var ms = new MemoryStream(encryptedBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);

        return await sr.ReadToEndAsync();
    }
}
