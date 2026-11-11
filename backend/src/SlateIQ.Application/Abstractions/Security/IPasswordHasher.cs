namespace SlateIQ.Application.Abstractions.Security;

public interface IPasswordHasher
{
    /// <summary>
    /// Hashes password using Argon2id with recommended parameters (ADR-008, SLQ-ARCH-010 §4.2):
    /// 3 iterations, 64 MB memory, 4 parallelism.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies candidate password against stored Argon2id hash using constant-time comparison.
    /// </summary>
    bool VerifyPassword(string candidatePassword, string storedHash);
}
