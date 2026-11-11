using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using SlateIQ.Application.Abstractions.Security;

namespace SlateIQ.Infrastructure.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int Iterations = 3;
    private const int MemorySizeKb = 65536; // 64 MB
    private const int DegreeOfParallelism = 4;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = ComputeArgon2id(password, salt);

        string saltB64 = Convert.ToBase64String(salt);
        string hashB64 = Convert.ToBase64String(hash);

        // Standard MCF format
        return $"$argon2id$v=19$m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}${saltB64}${hashB64}";
    }

    public bool VerifyPassword(string candidatePassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(candidatePassword) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 6 || parts[1] != "argon2id")
                return false;

            byte[] salt = Convert.FromBase64String(parts[4]);
            byte[] expectedHash = Convert.FromBase64String(parts[5]);

            byte[] actualHash = ComputeArgon2id(candidatePassword, salt);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ComputeArgon2id(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySizeKb
        };
        return argon2.GetBytes(HashSizeBytes);
    }
}
