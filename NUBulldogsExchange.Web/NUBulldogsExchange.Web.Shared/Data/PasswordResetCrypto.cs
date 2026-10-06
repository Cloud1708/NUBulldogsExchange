using System.Security.Cryptography;
using System.Text;

namespace NUBulldogsExchange.Web.Shared.Data;

/// <summary>Hashed OTP / reset-ticket helpers. Plaintext codes are never persisted.</summary>
public static class PasswordResetCrypto
{
    public const int CodeLength = 6;
    public const int CodeTtlSeconds = 600;
    public const int ResendCooldownSeconds = 60;
    public const int MaxVerifyAttempts = 5;
    public const int MaxSendsPerHour = 5;

    public static string GenerateNumericCode()
    {
        int value;
        do
        {
            value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        } while (value == 123456);

        return value.ToString("D6");
    }

    public static string HashSecret(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashCode(string email, string code) =>
        HashSecret($"{email}\n{code}");

    public static string NewResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool FixedEquals(string? left, string? right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(right ?? string.Empty);
        if (a.Length != b.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
