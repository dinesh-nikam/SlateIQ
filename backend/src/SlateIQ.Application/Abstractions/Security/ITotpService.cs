namespace SlateIQ.Application.Abstractions.Security;

public interface ITotpService
{
    /// <summary>
    /// Generates a new 160-bit Base32 secret key for TOTP MFA enrollment.
    /// </summary>
    string GenerateSecretKey();

    /// <summary>
    /// Verifies candidate 6-digit TOTP code against secret key within standard time window (NFR-SEC-003, TS-07).
    /// </summary>
    bool VerifyCode(string secretKey, string candidateCode);
}
