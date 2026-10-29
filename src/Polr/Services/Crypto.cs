using System.Security.Cryptography;
using System.Text;

namespace Polr.Services;

public static class Crypto
{
    const string Alphanumeric = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string RandomHex(int bytes) => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));

    public static string RandomString(int length) => RandomNumberGenerator.GetString(Alphanumeric, length);

    /// <summary>Constant-time comparison for secret keys and tokens.</summary>
    public static bool SecureEquals(string? a, string? b) =>
        a is not null && b is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    /// <summary>Checks a bcrypt hash, including the $2y$ hashes found in existing Polr databases.</summary>
    public static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
